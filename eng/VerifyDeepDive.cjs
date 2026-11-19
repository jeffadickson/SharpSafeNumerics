// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

// Browser verification uses an explicitly supplied existing Playwright module.
// No npm install or machine-specific module path is part of the document.
const fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url');
const assert=require('node:assert/strict');
async function main(){
  const modulePath=process.argv[2],out=process.argv[3];
  if(!modulePath||!out)throw Error('Usage: node eng/VerifyDeepDive.cjs <playwright-module-path> <artifact-directory>');
  if(!fs.existsSync(path.dirname(out))||!fs.statSync(path.dirname(out)).isDirectory())throw Error('Artifact parent missing');
  if(!fs.existsSync(out))fs.mkdirSync(out);
  const {chromium}=require(modulePath),browser=await chromium.launch({headless:true});
  try{
    const page=await browser.newPage({viewport:{width:1440,height:1000},reducedMotion:'reduce'}),errors=[],requests=[];
    page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>requests.push(r.url()));
    await page.coverage.startJSCoverage();
    await page.goto(pathToFileURL(process.argv[4] ? path.resolve(process.argv[4]) : path.resolve(__dirname,'../docs/interactive-deep-dive.html')).href);
    assert.equal(await page.evaluate(()=>window.demoVerification.passed),true);
    const fixtures=await page.evaluate(()=>window.demoVerification.count);assert(fixtures>2000);
    const text=id=>page.locator('#'+id).innerText();
    await page.locator('[data-preset="wide"]').click();
    assert.equal(await text('unchecked-value'),'0');assert.equal(await text('package-status'),'Overflow');
    for(const [policy,status,value] of [['Saturate','Success / ArithmeticError.None',(2n**128n-1n).toString()],['Wrap','Success / ArithmeticError.None','0'],['Throw','OverflowException','No result returned'],['Reject','Overflow','0']]){
      await page.selectOption('#a-policy',policy);assert.equal(await text('package-status'),status);assert.equal(await text('package-value'),value);
    }
    await page.locator('[data-preset="under"]').click();assert.equal(await text('package-status'),'Underflow');
    for(const bad of ['1e3','2.5','-','9'.repeat(161)]){await page.fill('#a-left',bad);assert((await text('a-error')).length>0);assert.equal(await text('package-value'),'—');}
    await page.locator('[data-preset="exact"]').click();assert.equal(await text('package-value'),'42');
    await page.fill('#c-input','-1');assert.equal(await text('c-truncate'),'255');assert.equal(await text('c-status'),'OutOfRange');
    await page.selectOption('#c-type','Int128');await page.fill('#c-input',(2n**127n-1n).toString());assert.equal(await text('c-status'),'None');
    await page.fill('#c-input',(2n**128n).toString());assert((await text('c-error')).length>0);
    await page.fill('#q-delta','2');assert((await text('q-try')).startsWith('OutOfRange'));assert((await text('q-sat')).includes('63'));assert.equal(await text('q-bytes'),'00 3E');
    await page.fill('#q-delta','2147483647');assert((await text('q-try')).startsWith('Overflow'));
    await page.fill('#q-delta','NaN');assert((await text('q-error')).length>0);await page.fill('#q-delta','2');
    for(let i=0;i<8;i++){await page.selectOption('#rule',String(i));assert((await text('rule-id')).startsWith('SSN100'+(i+1)));}
    await page.selectOption('#s-width','256');assert((await text('s-summary')).includes('8 in 256-bit groups'));
    for(const [contract,status] of [['overlap','OverlappingBuffers'],['length','LengthMismatch'],['small','DestinationTooSmall']]){await page.selectOption('#s-contract',contract);assert((await text('s-summary')).startsWith(status));assert.equal(await page.locator('.lane').count(),0);}
    await page.selectOption('#s-contract','valid');await page.selectOption('#s-width','128');assert((await text('s-summary')).includes('8 in 128-bit groups'));
    await page.selectOption('#s-width','0');assert((await text('s-summary')).includes('11 scalar'));
    await page.locator('#s-length').fill('0');assert((await text('s-summary')).includes('0 lanes'));assert.equal(await page.locator('.lane').count(),0);
    await page.selectOption('#s-width','256');await page.locator('#s-length').fill('12');assert((await text('s-summary')).includes('4 in 128-bit groups'));
    await page.locator('#s-length').fill('4');assert((await text('s-summary')).includes('widest executed 128'));
    await page.locator('#s-length').fill('11');await page.selectOption('#s-width','256');
    await page.locator('[data-preset="byte"]').click();
    await page.selectOption('#c-type','byte');await page.fill('#c-input','256');
    await page.selectOption('#rule','0');
    // Replace clipboard only in this isolated test page; preserve the user's clipboard.
    await page.evaluate(()=>Object.defineProperty(navigator,'clipboard',{configurable:true,value:{writeText:async text=>{window.testCopiedText=text;}}}));
    await page.locator('[data-copy="arithmetic-code"]').click();assert.equal(await page.locator('[data-copy="arithmetic-code"]').innerText(),'Copied');
    assert((await page.evaluate(()=>window.testCopiedText)).includes('SafeInteger.Add'));
    await page.evaluate(()=>{navigator.clipboard.writeText=async()=>{throw Error('Test denied');};});
    await page.locator('[data-copy="domain-code"]').click();assert((await page.locator('[data-copy="domain-code"]').innerText()).includes('Select and copy'));
    for(const width of [1440,390,320]){
      await page.setViewportSize({width,height:1000});
      assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Horizontal page overflow at '+width);
      const overlaps=await page.evaluate(()=>[...document.querySelectorAll('input,select,button')].filter(e=>{const r=e.getBoundingClientRect();return r.width>0&&(r.left<0||r.right>innerWidth+1)}).length);
      assert.equal(overlaps,0,'Control clipping at '+width);
      await page.screenshot({path:path.join(out,'deep-dive-'+width+'.png'),fullPage:true});
      await page.evaluate(()=>scrollTo(0,0));await page.screenshot({path:path.join(out,'viewport-'+width+'.png')});
    }
    await page.keyboard.press('Tab');assert(await page.evaluate(()=>document.activeElement!==document.body));
    assert.deepEqual(errors,[]);assert.equal(requests.filter(url=>!url.startsWith('file:')).length,0);
    const coverage=await page.coverage.stopJSCoverage();fs.writeFileSync(path.join(out,'browser-coverage.json'),JSON.stringify(coverage));
    // V8 ranges nest. Resolve the narrowest enclosing range for each disjoint
    // interval so an unexecuted branch overrides its executed containing function.
    let coveredBytes=0,totalBytes=0;
    for(const entry of coverage){
      totalBytes+=entry.source.length;
      const ranges=entry.functions.flatMap(f=>f.ranges),cuts=[...new Set(ranges.flatMap(r=>[r.startOffset,r.endOffset]))].sort((a,b)=>a-b);
      for(let i=0;i<cuts.length-1;i++){
        const containing=ranges.filter(r=>r.startOffset<=cuts[i]&&r.endOffset>=cuts[i+1]).sort((a,b)=>(a.endOffset-a.startOffset)-(b.endOffset-b.startOffset));
        if(containing[0]?.count>0)coveredBytes+=cuts[i+1]-cuts[i];
      }
    }
    const report={fixtures,browserVersion:browser.version(),nodeVersion:process.version,viewports:[1440,390,320],pageErrors:errors,externalRequests:0,checks:'arithmetic policies, 128-bit precision, malformed inputs, exact conversion, domain failures/codecs, all diagnostics, SIMD fallback/contracts, keyboard focus, layout',coverage:{coveredBytes,totalBytes,percent:Math.round(coveredBytes/totalBytes*10000)/100,scope:'V8 executed JavaScript character ranges; not statement, line, branch or .NET coverage'}};
    fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
  }finally{await browser.close();}
}
main().catch(e=>{console.error(e);process.exitCode=1;});

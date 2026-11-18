// SN-019-IEEE-CONTEXT: exact coefficient selection, not a refinement proof of C#.
module DecimalRounding {
  datatype Mode = NearestEven | NearestAway | TowardZero | TowardNegative | TowardPositive
  function Abs(x:int):int { if x<0 then -x else x }

  method RoundMagnitude(n:nat,d:nat,negative:bool,mode:Mode) returns (m:nat)
    requires d>0
    ensures n/d <= m <= n/d+1
    ensures n%d==0 ==> m==n/d
    ensures mode==TowardZero ==> m==n/d
    ensures mode==TowardPositive && negative ==> m==n/d
    ensures mode==TowardNegative && !negative ==> m==n/d
    ensures mode==TowardPositive && !negative ==> m*d>=n
    ensures mode==TowardNegative && negative ==> m*d>=n
    ensures mode==NearestEven || mode==NearestAway ==> 2*Abs(m*d-n)<=d
    ensures mode==NearestEven && 2*(n%d)==d ==> m%2==0
  {
    var q:=n/d;
    var r:=n%d;
    assert n==q*d+r;
    var up:=r!=0 &&
      ((mode==NearestEven && (2*r>d || (2*r==d && q%2!=0))) ||
       (mode==NearestAway && 2*r>=d) ||
       (mode==TowardPositive && !negative) ||
       (mode==TowardNegative && negative));
    m:=if up then q+1 else q;
    if up { assert m*d-n==d-r; }
    else { assert n-m*d==r; }
  }

  method Main() {
    var even:=RoundMagnitude(5,2,false,NearestEven); expect even==2;
    var away:=RoundMagnitude(5,2,false,NearestAway); expect away==3;
    var floorNegative:=RoundMagnitude(5,2,true,TowardNegative); expect floorNegative==3;
    var ceilingNegative:=RoundMagnitude(5,2,true,TowardPositive); expect ceilingNegative==2;
    var exact:=RoundMagnitude(6,2,false,NearestEven); expect exact==3;
    print "Decimal coefficient rounding model checks passed.\n";
  }
}

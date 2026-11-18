/* Independent, verification-only fixture generator using GNU MPFR 4.2.1.
 * Compile against the system MPFR/GMP libraries; no library sources are copied.
 * Run at 1024 and 2048 bits and compare all 200-significant-digit output rows.
 * Stability is bounded numerical evidence, not a global error enclosure proof.
 * Domain/pole/signed-zero rules are checked separately against IEEE 754-2019 9.2.
 */
#include <mpfr.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct { const char *op, *x, *y; long n; int exact; } fixture;
static const fixture cases[] = {
 {"Exp","0.5","0",0,0}, {"ExpM1","0.5","0",0,0},
 {"Exp2","0.5","0",0,0}, {"Exp2M1","0.5","0",0,0},
 {"Exp10","0.5","0",0,0}, {"Exp10M1","0.5","0",0,0},
 {"Log","0.5","0",0,0}, {"LogP1","0.5","0",0,0},
 {"Log2","0.5","0",0,1}, {"Log2P1","0.5","0",0,0},
 {"Log10","0.5","0",0,0}, {"Log10P1","0.5","0",0,0},
 {"Sin","0.5","0",0,0}, {"Cos","0.5","0",0,0}, {"Tan","0.5","0",0,0},
 {"Asin","0.5","0",0,0}, {"Acos","0.5","0",0,0}, {"Atan","0.5","0",0,0},
 {"Sinh","0.5","0",0,0}, {"Cosh","0.5","0",0,0}, {"Tanh","0.5","0",0,0},
 {"Asinh","-0.5","0",0,0}, {"Acosh","2","0",0,0}, {"Atanh","0.5","0",0,0},
 {"Cbrt","2","0",0,0},
 {"SinPi","0.125","0",0,0}, {"CosPi","0.125","0",0,0}, {"TanPi","0.125","0",0,0},
 {"AsinPi","0.5","0",0,0}, {"AcosPi","0.5","0",0,0}, {"AtanPi","0.5","0",0,0},
 {"Atan2","0.5","2",0,0}, {"Atan2Pi","-0.5","-2",0,0},
 {"Pow","2","0.5",0,0}, {"PowR","2","0.5",0,0}, {"PowN","0.5","0",3,1},
 {"RootN","2","0",3,0}, {"Compound","0.5","0",3,1},
 {"Hypot","1","2",0,0}, {"ReciprocalSqrt","2","0",0,0},
 {"ExpM1","1e-20","0",0,0}, {"LogP1","1e-20","0",0,0},
 {"Exp2M1","1e-20","0",0,0}, {"Log2P1","1e-20","0",0,0},
 {"Exp10M1","1e-20","0",0,0}, {"Log10P1","1e-20","0",0,0},
 {"Sin","-0.5","0",0,0}, {"Atan","2","0",0,0},
 {"Asin","-1","0",0,0}, {"Acos","-1","0",0,0},
 {"Atan2","-0.5","-2",0,0}, {"Asinh","0.5","0",0,0},
 {"RootN","2","0",-3,0}, {"Pow","0.5","-0.5",0,0},
 {"Acos","0","0",0,0}, {"Atan","inf","0",0,0}, {"Atan","-inf","0",0,0},
 {"Atan2","inf","inf",0,0}, {"Atan2","inf","-inf",0,0},
 {"Atan2","-inf","inf",0,0}, {"Atan2","-inf","-inf",0,0},
 {"Atan2","1","0",0,0}, {"Atan2","-1","0",0,0},
 {"Atan2","0","-1",0,0}, {"Atan2","-0","-1",0,0},
 {"Atan2Pi","1","1",0,1}, {"Atan2Pi","-1","-1",0,1},
 {"AtanPi","1","0",0,1}, {"AsinPi","1","0",0,1}, {"AcosPi","-1","0",0,1},
 {"PowN","1.000001","0",100001,0}, {"PowN","-1.000001","0",100001,0},
 {"Pow","1.000001","100001",0,0}, {"Pow","-1.000001","100001",0,0},
 {"Compound","0.000001","0",100001,0}, {"Compound","-0.000001","0",100001,0},
 {"Compound","0.000001","0",-100001,0}, {"RootN","2","0",10001,0},
 {"RootN","-2","0",-3,0}
};

static const fixture internal_cases[] = {
 {"Pi","0","0",0,0},
 {"Exp","-2","0",0,0}, {"Exp","-0.125","0",0,0}, {"Exp","0.125","0",0,0}, {"Exp","2","0",0,0}, {"Exp","10","0",0,0},
 {"Log","0.125","0",0,0}, {"Log","2","0",0,0}, {"Log","10","0",0,0},
 {"Atan","-2","0",0,0}, {"Atan","-0.125","0",0,0}, {"Atan","0.125","0",0,0}, {"Atan","2","0",0,0}, {"Atan","10","0",0,0},
 {"Sin","-2","0",0,0}, {"Sin","-0.125","0",0,0}, {"Sin","0.125","0",0,0}, {"Sin","2","0",0,0}, {"Sin","10","0",0,0},
 {"Cos","-2","0",0,0}, {"Cos","-0.125","0",0,0}, {"Cos","0.125","0",0,0}, {"Cos","2","0",0,0}, {"Cos","10","0",0,0}
};

static void evaluate(mpfr_t r, const fixture *f, mpfr_t x, mpfr_t y, mpfr_rnd_t rounding) {
 const char *op=f->op;
 if (!strcmp(op,"Pi")) { mpfr_const_pi(r,rounding); return; }
 #define U(name, fn) if (!strcmp(op,name)) { fn(r,x,rounding); return; }
 U("Exp",mpfr_exp) U("ExpM1",mpfr_expm1) U("Exp2",mpfr_exp2) U("Exp2M1",mpfr_exp2m1)
 U("Exp10",mpfr_exp10) U("Exp10M1",mpfr_exp10m1)
 U("Log",mpfr_log) U("LogP1",mpfr_log1p) U("Log2",mpfr_log2) U("Log2P1",mpfr_log2p1)
 U("Log10",mpfr_log10) U("Log10P1",mpfr_log10p1)
 U("Sin",mpfr_sin) U("Cos",mpfr_cos) U("Tan",mpfr_tan)
 U("Asin",mpfr_asin) U("Acos",mpfr_acos) U("Atan",mpfr_atan)
 U("Sinh",mpfr_sinh) U("Cosh",mpfr_cosh) U("Tanh",mpfr_tanh)
 U("Asinh",mpfr_asinh) U("Acosh",mpfr_acosh) U("Atanh",mpfr_atanh) U("Cbrt",mpfr_cbrt)
 U("SinPi",mpfr_sinpi) U("CosPi",mpfr_cospi) U("TanPi",mpfr_tanpi)
 U("AsinPi",mpfr_asinpi) U("AcosPi",mpfr_acospi) U("AtanPi",mpfr_atanpi)
 U("ReciprocalSqrt",mpfr_rec_sqrt)
 #undef U
 if (!strcmp(op,"Atan2")) mpfr_atan2(r,x,y,rounding);
 else if (!strcmp(op,"Atan2Pi")) mpfr_atan2pi(r,x,y,rounding);
 else if (!strcmp(op,"Pow")) mpfr_pow(r,x,y,rounding);
 else if (!strcmp(op,"PowR")) mpfr_powr(r,x,y,rounding);
 else if (!strcmp(op,"PowN")) mpfr_pow_si(r,x,f->n,rounding);
 else if (!strcmp(op,"RootN")) mpfr_rootn_si(r,x,f->n,rounding);
 else if (!strcmp(op,"Compound")) mpfr_compound_si(r,x,f->n,rounding);
 else if (!strcmp(op,"Hypot")) mpfr_hypot(r,x,y,rounding);
 else { fprintf(stderr,"Unknown operation %s\n",op); exit(2); }
}

/* Endpoint orientation is specific to the bounded fixture domains above, not
 * a general interval engine. All uncertain-input cases are monotonic there.
 * Binary second operands are required to parse exactly at the selected precision.
 */
static int decreasing(const fixture *f) {
 const char *op=f->op;
 return !strcmp(op,"Cos") || !strcmp(op,"Acos") || !strcmp(op,"CosPi") ||
   !strcmp(op,"AcosPi") || !strcmp(op,"ReciprocalSqrt") ||
   ((!strcmp(op,"RootN") || !strcmp(op,"Compound")) && f->n<0) ||
   ((!strcmp(op,"Pow") || !strcmp(op,"PowR")) && f->y[0]=='-') ||
   ((!strcmp(op,"Atan2") || !strcmp(op,"Atan2Pi")) && f->y[0]=='-');
}

int main(int argc, char **argv) {
 if (argc != 2 && argc != 3) return 2;
 int internal=argc==3 && !strcmp(argv[2],"internal"), bounds=argc==3;
 if (bounds && !internal && strcmp(argv[2],"bounds")) return 2;
 char *end; long precision=strtol(argv[1],&end,10);
 if (*end || precision<1024 || precision>8192) return 2;
 mpfr_t x,y,result,xlo,xhi,ylo,yhi,lo,hi;
 mpfr_inits2(precision,x,y,result,xlo,xhi,ylo,yhi,lo,hi,(mpfr_ptr)0);
 size_t count=internal?sizeof(internal_cases)/sizeof(internal_cases[0]):sizeof(cases)/sizeof(cases[0]);
 const fixture *selected=internal?internal_cases:cases;
 for (size_t i=0;i<count;i++) {
   const fixture *f=&selected[i];
   if (mpfr_set_str(x,f->x,10,MPFR_RNDN) || mpfr_set_str(y,f->y,10,MPFR_RNDN)) return 2;
   evaluate(result,f,x,y,MPFR_RNDN);
   printf("%s|%s|%s|%ld|%d|",f->op,f->x,f->y,f->n,f->exact);
   if (internal) mpfr_printf("%.79Re",result); else mpfr_printf("%.199Re",result);
   if (bounds) {
     if (mpfr_set_str(xlo,f->x,10,MPFR_RNDD) || mpfr_set_str(xhi,f->x,10,MPFR_RNDU) ||
         mpfr_set_str(ylo,f->y,10,MPFR_RNDD) || mpfr_set_str(yhi,f->y,10,MPFR_RNDU)) return 2;
     if (mpfr_cmp(ylo,yhi)) { fprintf(stderr,"Second argument is not exact\n"); return 2; }
     if (internal && mpfr_cmp(xlo,xhi)) { fprintf(stderr,"Internal fixture input must be exact\n"); return 2; }
     evaluate(lo,f,decreasing(f)?xhi:xlo,ylo,MPFR_RNDD);
     evaluate(hi,f,decreasing(f)?xlo:xhi,yhi,MPFR_RNDU);
     if (mpfr_greater_p(lo,hi)) { fprintf(stderr,"Invalid bound orientation\n"); return 2; }
     if (internal) mpfr_printf("|%.79RDe|%.79RUe",lo,hi); else mpfr_printf("|%.199RDe|%.199RUe",lo,hi);
   }
   putchar('\n');
 }
 mpfr_clears(x,y,result,xlo,xhi,ylo,yhi,lo,hi,(mpfr_ptr)0);
 return 0;
}

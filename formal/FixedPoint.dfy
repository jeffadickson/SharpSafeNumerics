// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

// SN-018-FIXED: mathematical Q32.32 model, not a C# refinement proof.
module FixedPoint {
  datatype Error = Ok | DivideByZero | Inexact | BelowRange | AboveRange
  const Scale: int := 4294967296
  const Minimum: int := -9223372036854775808
  const Maximum: int := 9223372036854775807

  function Abs(x: int): int { if x < 0 then -x else x }

  // Magnitudes make truncation agree with C# for negative rationals.
  method Round(n: int, d: int, even: bool) returns (v: int, remainder: int)
    requires d > 0
    ensures 0 <= remainder < d
    ensures !even ==> Abs(v*d-n) == remainder
    ensures !even ==> Abs(v*d-n) < d
    ensures even ==> 2*Abs(v*d-n) <= d
    ensures even && 2*remainder == d ==> v % 2 == 0
  {
    var a := Abs(n);
    var q := a / d;
    remainder := a % d;
    assert a == q*d + remainder;
    var up := even && (2*remainder > d || (2*remainder == d && q%2 != 0));
    var m := if up then q+1 else q;
    v := if n < 0 then -m else m;
    if up { assert m*d-a == d-remainder; }
    else { assert a-m*d == remainder; }
  }

  // Exact mode rejects discarded fractions; successful raw outputs fit Int64.
  // A zero output on every failure models the public Try contract.
  method Divide(left: int, right: int, exact: bool, even: bool)
      returns (error: Error, value: int)
    requires Minimum <= left <= Maximum
    requires Minimum <= right <= Maximum
    ensures error != Ok ==> value == 0
    ensures error == Ok ==> Minimum <= value <= Maximum
    ensures right == 0 ==> error == DivideByZero
    ensures error == Ok && exact ==> value*right == left*Scale
    ensures error == Ok && !exact && even ==>
      2*Abs(value*right-left*Scale) <= Abs(right)
    ensures error == Ok && !exact && !even ==>
      Abs(value*right-left*Scale) < Abs(right)
  {
    value := 0;
    if right == 0 { error := DivideByZero; return; }
    var n := left*Scale;
    if right < 0 { n := -n; }
    var d := Abs(right);
    var rounded, rem := Round(n,d, !exact && even);
    if exact && rem != 0 { error := Inexact; return; }
    if rounded < Minimum { error := BelowRange; return; }
    if rounded > Maximum { error := AboveRange; return; }
    value := rounded;
    if right < 0 {
      assert value*right-left*Scale == -(value*d-n);
      assert Abs(value*right-left*Scale) == Abs(value*d-n);
    } else {
      assert value*right-left*Scale == value*d-n;
    }
    error := Ok;
  }

  method Main() {
    var a,r := Round(5,2,true); expect a == 2 && r == 1;
    a,r := Round(7,2,true); expect a == 4;
    a,r := Round(-5,2,true); expect a == -2;
    a,r := Round(-7,2,true); expect a == -4;
    a,r := Round(-7,2,false); expect a == -3;
    var e,v := Divide(Scale,3*Scale,true,false);
    expect e == Inexact && v == 0;
    e,v := Divide(Scale,0,false,true);
    expect e == DivideByZero && v == 0;
    e,v := Divide(Maximum,1,false,true);
    expect e == AboveRange && v == 0;
    e,v := Divide(Minimum,1,false,true);
    expect e == BelowRange && v == 0;
    e,v := Divide(Scale,2*Scale,true,false);
    expect e == Ok && v == Scale/2;
    print "FixedPoint model: 10 executable checks passed\n";
  }
}


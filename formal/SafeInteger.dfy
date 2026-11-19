// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

datatype ArithmeticError = None | Overflow | Underflow | DivideByZero | OutOfRange

const MIN32: int := -2147483648
const MAX32: int := 2147483647

// Model range-valid policy bytes. The C# validation implementation is tested separately.
method ValidatePolicyByte(policy: int) returns (valid: bool)
  requires 0 <= policy <= 255
  ensures valid <==> 0 <= policy <= 3
{
  valid := policy <= 3;
}

// Signed C# integer division truncates toward zero; Dafny's / alone is Euclidean.
function TruncateDivide(a: int, b: int): int
  requires b != 0
{
  if a < 0 then
    (if b < 0 then (-a) / (-b) else -((-a) / b))
  else
    (if b < 0 then -(a / (-b)) else a / b)
}

method SaturateInteger(exact: int, minimum: int, maximum: int) returns (value: int)
  requires minimum <= maximum
  ensures minimum <= value <= maximum
  ensures exact < minimum ==> value == minimum
  ensures exact > maximum ==> value == maximum
  ensures minimum <= exact <= maximum ==> value == exact
{
  value := if exact < minimum then minimum else if exact > maximum then maximum else exact;
}

method WrapInteger(exact: int, minimum: int, maximum: int) returns (value: int, quotient: int)
  requires minimum <= maximum
  ensures minimum <= value <= maximum
  ensures exact - value == quotient * (maximum - minimum + 1)
{
  var width := maximum - minimum + 1;
  var displacement := exact - minimum;
  quotient := displacement / width;
  value := minimum + displacement % width;
  assert displacement == (displacement / width) * width + displacement % width;
  assert value - exact == -(displacement / width) * width;
  assert exact - value == (displacement / width) * width;
}

// Mathematical support for the retained zero-guard mutant dispositions; not C# equivalence.
lemma ZeroGuards(value: int, minimum: int, maximum: int)
  requires minimum <= value <= maximum
  ensures !(value > maximum - 0)
  ensures !(value < minimum + 0)
  ensures value * 0 == 0 && 0 * value == 0
{}

// Mathematical range-admission model only; no linking proof to the C# algorithm.
method TryConvertInteger(input: int, minimum: int, maximum: int)
    returns (error: ArithmeticError, value: int)
  requires minimum <= maximum
  ensures error == None <==> minimum <= input <= maximum
  ensures error == None ==> value == input
  ensures error != None ==> error == OutOfRange && value == 0
{
  if input < minimum || input > maximum {
    error, value := OutOfRange, 0;
  } else {
    error, value := None, input;
  }
}

method TryAdd32(a: int, b: int) returns (error: ArithmeticError, value: int)
  ensures error == None ==> MIN32 <= value <= MAX32 && value == a + b
  ensures error == Overflow ==> a + b > MAX32 && value == 0
  ensures error == Underflow ==> a + b < MIN32 && value == 0
{
  var exact := a + b;
  if exact > MAX32 {
    error, value := Overflow, 0;
  } else if exact < MIN32 {
    error, value := Underflow, 0;
  } else {
    error, value := None, exact;
  }
}

method TrySubtract32(a: int, b: int) returns (error: ArithmeticError, value: int)
  ensures error == None ==> MIN32 <= value <= MAX32 && value == a - b
  ensures error == Overflow ==> a - b > MAX32 && value == 0
  ensures error == Underflow ==> a - b < MIN32 && value == 0
{
  var exact := a - b;
  if exact > MAX32 {
    error, value := Overflow, 0;
  } else if exact < MIN32 {
    error, value := Underflow, 0;
  } else {
    error, value := None, exact;
  }
}

method TryMultiply32(a: int, b: int) returns (error: ArithmeticError, value: int)
  ensures error == None ==> MIN32 <= value <= MAX32 && value == a * b
  ensures error == Overflow ==> a * b > MAX32 && value == 0
  ensures error == Underflow ==> a * b < MIN32 && value == 0
{
  var exact := a * b;
  if exact > MAX32 {
    error, value := Overflow, 0;
  } else if exact < MIN32 {
    error, value := Underflow, 0;
  } else {
    error, value := None, exact;
  }
}

method TryDivide32(a: int, b: int) returns (error: ArithmeticError, value: int)
  requires MIN32 <= a <= MAX32 && MIN32 <= b <= MAX32
  ensures b == 0 ==> error == DivideByZero
  ensures b != 0 && a == MIN32 && b == -1 ==> error == Overflow
  ensures error == None <==> b != 0 && !(a == MIN32 && b == -1)
  ensures error == None ==> value == TruncateDivide(a, b)
  ensures error != None ==> value == 0
{
  if b == 0 {
    error, value := DivideByZero, 0;
  } else if a == MIN32 && b == -1 {
    error, value := Overflow, 0;
  } else {
    error, value := None, TruncateDivide(a, b);
  }
}

method TryBoundedAdd(value: int, delta: int, minimum: int, maximum: int)
    returns (error: ArithmeticError, result: int)
  requires minimum <= value <= maximum
  requires minimum <= maximum
  ensures error == None ==> minimum <= result <= maximum && result == value + delta
  ensures error == OutOfRange ==> (value + delta < minimum || value + delta > maximum) && result == 0
{
  var exact := value + delta;
  if exact < minimum || exact > maximum {
    error, result := OutOfRange, 0;
  } else {
    error, result := None, exact;
  }
}

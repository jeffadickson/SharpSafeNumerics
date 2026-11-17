datatype ArithmeticError = None | Overflow | Underflow | DivideByZero | OutOfRange

const MIN32: int := -2147483648
const MAX32: int := 2147483647

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
  ensures b == 0 ==> error == DivideByZero
  ensures b != 0 && a == MIN32 && b == -1 ==> error == Overflow
{
  if b == 0 {
    error, value := DivideByZero, 0;
  } else if a == MIN32 && b == -1 {
    error, value := Overflow, 0;
  } else {
    error, value := None, a / b;
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

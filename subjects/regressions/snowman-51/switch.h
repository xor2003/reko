// switch.h
// Generated by decompiling D:\dev\uxmal\reko\master\subjects\regressions\snowman-51\switch.dll
// using Decompiler version 0.5.5.0.

/*
// Equivalence classes ////////////
Eq_1: (struct "Globals" (10072000 (str char) str10072000) (10072010 (str char) str10072010) (10072014 (str char) str10072014) (10072018 (str char) str10072018))
	globals_t (in globals : (ptr (struct "Globals")))
Eq_12: BOOL
	T_12 (in eax : Eq_12)
	T_16 (in 0x00000001 : word32)
Eq_13: HANDLE
	T_13 (in hModule : Eq_13)
Eq_14: DWORD
	T_14 (in dwReason : Eq_14)
Eq_15: LPVOID
	T_15 (in lpReserved : Eq_15)
// Type Variables ////////////
globals_t: (in globals : (ptr (struct "Globals")))
  Class: Eq_1
  DataType: (ptr Eq_1)
  OrigDataType: (ptr (struct "Globals"))
T_2: (in eax : (ptr char))
  Class: Eq_2
  DataType: (ptr char)
  OrigDataType: (ptr char)
T_3: (in n : uint32)
  Class: Eq_3
  DataType: uint32
  OrigDataType: uint32
T_4: (in 0xFFFFFFFE : word32)
  Class: Eq_3
  DataType: uint32
  OrigDataType: uint32
T_5: (in n > 0xFFFFFFFE : bool)
  Class: Eq_5
  DataType: bool
  OrigDataType: bool
T_6: (in 0x10072000 : ptr32)
  Class: Eq_2
  DataType: (ptr char)
  OrigDataType: ptr32
T_7: (in 0x00000001 : word32)
  Class: Eq_7
  DataType: word32
  OrigDataType: word32
T_8: (in n + 0x00000001 : word32)
  Class: Eq_8
  DataType: word32
  OrigDataType: word32
T_9: (in 0x10072018 : ptr32)
  Class: Eq_2
  DataType: (ptr char)
  OrigDataType: ptr32
T_10: (in 0x10072014 : ptr32)
  Class: Eq_2
  DataType: (ptr char)
  OrigDataType: ptr32
T_11: (in 0x10072010 : ptr32)
  Class: Eq_2
  DataType: (ptr char)
  OrigDataType: ptr32
T_12: (in eax : Eq_12)
  Class: Eq_12
  DataType: Eq_12
  OrigDataType: BOOL
T_13: (in hModule : Eq_13)
  Class: Eq_13
  DataType: Eq_13
  OrigDataType: HANDLE
T_14: (in dwReason : Eq_14)
  Class: Eq_14
  DataType: Eq_14
  OrigDataType: DWORD
T_15: (in lpReserved : Eq_15)
  Class: Eq_15
  DataType: Eq_15
  OrigDataType: LPVOID
T_16: (in 0x00000001 : word32)
  Class: Eq_12
  DataType: Eq_12
  OrigDataType: word32
*/
typedef struct Globals {
	char str10072000[];	// 10072000
	char str10072010[];	// 10072010
	char str10072014[];	// 10072014
	char str10072018[];	// 10072018
} Eq_1;

typedef BOOL Eq_12;

typedef HANDLE Eq_13;

typedef DWORD Eq_14;

typedef LPVOID Eq_15;


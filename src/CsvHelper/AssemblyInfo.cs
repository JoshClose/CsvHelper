using System;

// Declared as CsvHelper 33 did, so that assemblies that are CLS-compliant themselves can use CsvHelper's types in their
// public API without warnings. Public API must avoid unsigned types, UInt128 and pointers, and attributes need a
// constructor without array parameters (the compiler warns otherwise).
[assembly: CLSCompliant(true)]

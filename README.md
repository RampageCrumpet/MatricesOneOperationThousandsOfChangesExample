# Matrix vs Iterative Tax Calculation Demo

A small C# demo comparing a straightforward per-employee (iterative) tax calculation with a matrix-based formulation using MathNet.Numerics and Intel MKL.

The project benchmarks both approaches in Release mode and validates numerical equivalence via max-delta checks.

Run with:
`dotnet run -c Release`

Adjust employee count and general-ledger bucket count in the entry point to observe where matrix multiplication helps and where memory bandwidth dominates.

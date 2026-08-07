using System.Runtime.CompilerServices;

// ChargeSystem.Consume() is internal so that only BankSystem can zero the pool
// (docs/01_ARCHITECTURE.md §6). The EditMode suite has to be able to see it to
// assert FR-205, so the test assembly is the single friend assembly.
[assembly: InternalsVisibleTo("Overdraft.Tests")]

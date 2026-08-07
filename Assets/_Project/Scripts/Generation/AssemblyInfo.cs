using System.Runtime.CompilerServices;

// Generation/ has its own asmdef boundary, so the InternalsVisibleTo declaration for
// Overdraft.Runtime (in Scripts/AssemblyInfo.cs) does not cover this assembly. A small number
// of internal helpers (e.g. WaveGenerator.Shape, WaveGenerator.PickPattern) are exposed to the
// test suite so determinism and clump-shape properties can be asserted directly rather than
// only inferred statistically through Generate()'s public surface.
[assembly: InternalsVisibleTo("Overdraft.Tests")]

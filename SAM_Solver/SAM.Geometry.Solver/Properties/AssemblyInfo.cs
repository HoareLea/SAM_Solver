// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Expose internal helpers (e.g. SnapSolver.AreParallelWithinTolerance) to the unit-test assembly.
[assembly: InternalsVisibleTo("SAM.Solver.Tests")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("dfa00ad8-8169-4e3c-943a-05f6f2196e3a")]

// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using Xunit;

// The solver keeps mutable static state (tolerances, the PerpendicularMergeTolerance / SolverWarnings
// statics), so run tests serially to avoid cross-test interference.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

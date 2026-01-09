[![Build (Windows)](https://github.com/SAM-BIM/SAM_Solver/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/SAM-BIM/SAM_Solver/actions/workflows/build.yml)
[![Installer (latest)](https://img.shields.io/github/v/release/SAM-BIM/SAM_Deploy?label=installer)](https://github.com/SAM-BIM/SAM_Deploy/releases/latest)

# SAM_Solver

<a href="https://github.com/SAM-BIM/SAM">
  <img src="https://github.com/SAM-BIM/SAM/blob/master/Grasshopper/SAM.Core.Grasshopper/Resources/SAM_Small.png"
       align="left" hspace="10" vspace="6">
</a>

**SAM_Solver** is part of the **SAM (Sustainable Analytical Model) Toolkit**.

This repository provides **geometric and topological solver utilities**
used to prepare, manipulate, and enclose analytical models within SAM workflows.

The solver includes methods to support operations such as:
- snapping and alignment
- extension and trimming
- merging and resolving geometry
- preparing closed and analysis-ready model representations

These utilities are intended to support downstream analytical and simulation workflows
by ensuring consistent, valid, and well-formed analytical geometry.

---

## Scope

`SAM_Solver` focuses on **low-level model preparation and resolution logic** and is designed
to be used internally by other SAM modules rather than as a user-facing component.

Typical use cases include:
- preparing analytical geometry prior to simulation
- resolving intersections and overlaps
- enforcing geometric consistency across model elements

---

## Resources
- 🧠 **SAM Core:** https://github.com/SAM-BIM/SAM  
- 🧰 **Installers:** https://github.com/SAM-BIM/SAM_Deploy  

---

## Development notes

- Target framework: **.NET / C#**
- Solver logic follows SAM-BIM analytical modelling conventions
- New or modified `.cs` files must include the SPDX header from `COPYRIGHT_HEADER.txt`

---

## Licence

This repository is maintained as part of the SAM toolkit.

Each contributor retains copyright to their respective contributions.  
The project history (Git) records authorship and provenance of all changes.

See:
- `LICENSE`
- `NOTICE`
- `COPYRIGHT_HEADER.txt`

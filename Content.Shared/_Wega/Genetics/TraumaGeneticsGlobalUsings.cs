// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// The imported Trauma genetics sources were originally compiled in their own assembly and
// relied on that project's global usings. The hybrid integration compiles only those sources
// into Content.Shared, so provide the same Robust namespaces without importing the project.
global using Robust.Shared.GameStates;
global using Robust.Shared.Network;
global using Robust.Shared.Prototypes;
global using Robust.Shared.Replays;
global using Robust.Shared.Serialization;

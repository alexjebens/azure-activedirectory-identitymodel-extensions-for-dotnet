﻿// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Globalization is not used in project")]
[assembly: SuppressMessage("Design", "CA1052:Static holder types should be Static or NotInheritable", Justification = "Previously released as non-static / inheritable", Scope = "type", Target = "~T:Microsoft.IdentityModel.Logging.LogHelper")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Execution should not be altered for exceptions on format", Scope = "member", Target = "~M:Microsoft.IdentityModel.Logging.IdentityModelEventSource.PrepareMessage(System.Diagnostics.Tracing.EventLevel,System.String,System.Object[])~System.String")]

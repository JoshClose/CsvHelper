// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

[assembly: AssemblyProduct( "CsvHelper" )]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible( false )]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid( "2bff163b-a135-4068-be75-9a9464f3250f" )]

// DO NOT update this.
// This only gets a bump on major releases.
[assembly: AssemblyVersion( "3.0.0.0" )]

// This is now set from the .csproj file.
// Update this.
//[assembly: AssemblyFileVersion( "2.16.0.0" )]

[assembly: CLSCompliant( true )]

[assembly: InternalsVisibleTo( "CsvHelper.Tests, PublicKey=00240000048000009400000006020000002400005253413100040000010001000db97564beef98ad18a76ba31f769fab92b14341c9c37ed12f8004bb2a1a7fe42ad829b0e285915a816f05a32325c5e0ba83bd69d8f4d26a0785ccf446749842ad038f7325601a99c59a323dfa7ecf210139159da0aad1822b5d9c9be6d914ecbaa8b8c908c4af798a89b8777010971d81975079a49662ced398c742ff186a94" )]

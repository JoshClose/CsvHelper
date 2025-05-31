// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper

return await Bootstrapper.Factory
	.CreateWeb(args)
	// The next line is necessary to fix uppercase/lowercase mismatches between
	// filenames and URLs in API docs which cause 404s on Linux/GitHub Pages.
	.AddSetting(WebKeys.OptimizeContentFileNames, false)
	.AddDocs()
	.AddSourceFiles(@"../../CsvHelper/**/{!.git,!bin,!obj,}/**/*.cs")
	.RunAsync();

# CsvHelper

**Note:** Documentation will be update daily until it's complete. I didn't want the release of 3.0.0 to be pushed back any more due to documentation.

A library for reading and writing CSV files. Extremely fast, flexible, and easy to use. Supports reading and writing of custom class objects. CSV helper implements [RFC 4180](https://tools.ietf.org/html/rfc4180). By default, it's very conservative in its writing, but very liberal in its reading. There is a large set of configuration that can be done to change how reading and writing behaves, giving you the ability read/write non-standard files also.

## Installation

### Package Manager Console

```
PM> Install-Package CsvHelper
```

### .NET CLI Console

```
> dotnet add package CsvHelper
```

## Documentation

[http://joshclose.github.io/CsvHelper](http://joshclose.github.io/CsvHelper)

## License

Dual licensed. Choose whichever license suits your needs.

[Microsoft Public License (MS-PL)](http://www.opensource.org/licenses/MS-PL)

[Apache License, Version 2.0](http://opensource.org/licenses/Apache-2.0)

## Contributions

Want to contribute? Great! Here are a few guidelines.
1. If you want to do a feature, post an issue about the feature first. Some features are intentionally left out, some features may already be in the works, or I may have some advice on how I think it should be done. I would feel bad if time was spent on some code that won't be used.
2. If you want to do a bug fix, it might not be a bad idea to post about it too. I've had the same bug fixed by multiple people at the same time before.
3. All code should have a unit test. If you make a feature, there should be significant tests around the feature. If you do a bug fix, there should be a test specific to that bug so it doesn't happen again.
4. Pull requests should have a single commit. If you have multiple commits, squash them into a single commit before requesting a pull.
5. Try and follow the code styling already in place.

Looking for 2.x documentation? You can find it [here](/CsvHelper/2.x).

<br/>
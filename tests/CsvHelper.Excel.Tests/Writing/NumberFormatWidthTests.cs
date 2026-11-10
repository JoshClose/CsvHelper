using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CsvHelper.Excel.Tests.Corpus;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// The text and the width of numbers, dates and times in Excel's format codes and in General
/// (<see cref="NumberFormatWidth"/>), against what Excel 365 (16.0 build 20527, en-US, 144 DPI) showed through COM on
/// 2026-11-09 in a workbook whose Normal style is Calibri 11: each value's <c>Text</c>, and the width in points of its
/// column after <c>AutoFit</c>. The probes are in <c>Csv\_bench\results\phase5\step3</c> (<c>general.ps1</c>,
/// <c>formats.ps1</c>).
/// </summary>
/// <remarks>
/// A width is the text's widest form at 96 DPI with Excel's padding, in points. Excel lays text out at 144 DPI and rounds
/// a column to whole pixels there, so a width can be up to a pixel at 144 DPI, half a point, narrower than Excel's; never
/// more.
/// </remarks>
public class NumberFormatWidthTests
{
	/// <summary>Half a point: a pixel at 144 DPI, by which Excel's rounding of a width can pass ours.</summary>
	private const double Rounding = 0.5;

	/// <summary>Each number, and its text and width in General: <c>value 'text'@points</c>.</summary>
	private const string General = """
		0	'0'@10
		1	'1'@10
		-1	'-1'@13.5
		7	'7'@10
		12	'12'@15.5
		123	'123'@21
		1234	'1234'@26.5
		12345	'12345'@32
		123456	'123456'@37.5
		1234567	'1234567'@43
		12345678	'12345678'@48.5
		123456789	'123456789'@54
		1234567890	'1234567890'@59.5
		12345678901	'12345678901'@65
		99999999999	'99999999999'@65
		-99999999999	'-99999999999'@68.5
		123456789012	'1.23457E+11'@65
		999999999999	'1E+12'@32
		100000000000	'1E+11'@32
		1234567890123	'1.23457E+12'@65
		123456789012345	'1.23457E+14'@65
		1234567890123456	'1.23457E+15'@65
		12345678901234567890	'1.23457E+19'@65
		1E+15	'1E+15'@32
		1E+16	'1E+16'@32
		1.5E+20	'1.5E+20'@43
		1E+100	'1E+100'@37.5
		1.23456789E+100	'1.2346E+100'@65
		-1.23456789E+100	'-1.2346E+100'@68.5
		1.7976931348623157E+308	'1.7977E+308'@65
		99999999999.4	'99999999999'@65
		99999999999.5	'1E+11'@32
		99999999999.9	'1E+11'@32
		9999999999.95	'10000000000'@65
		9999999999.94	'10000000000'@65
		0.99999999999	'1'@10
		9.99999999999	'10'@15.5
		-9.99999999999	'-10'@19
		0.1	'0.1'@21
		0.5	'0.5'@21
		0.30000000000000004	'0.3'@21
		0.3333333333333333	'0.333333333'@65
		0.6666666666666666	'0.666666667'@65
		-0.3333333333333333	'-0.333333333'@68.5
		3.14159265358979	'3.141592654'@65
		1234.5678901234	'1234.56789'@59.5
		-1234.5678901234	'-1234.56789'@63
		123456.123456	'123456.1235'@65
		1234567.891	'1234567.891'@65
		-1234567.891	'-1234567.891'@68.5
		12345678.9	'12345678.9'@59.5
		123456789.123	'123456789.1'@65
		1234567890.12345	'1234567890'@59.5
		0.25	'0.25'@26.5
		0.125	'0.125'@32
		0.0625	'0.0625'@37.5
		0.001	'0.001'@32
		0.0001	'0.0001'@37.5
		0.00001	'0.00001'@43
		0.000001	'0.000001'@48.5
		0.0000001	'0.0000001'@54
		0.00000001	'0.00000001'@59.5
		0.000000001	'0.000000001'@65
		0.0000000001	'1E-10'@32
		1.5E-05	'0.000015'@48.5
		2.5E-05	'0.000025'@48.5
		-1E-05	'-0.00001'@46.5
		1.234E-05	'0.00001234'@59.5
		1.2345E-05	'0.000012345'@65
		1.23456E-05	'1.23456E-05'@65
		0.000123456789	'0.000123457'@65
		-0.000123456789	'-0.000123457'@68.5
		0.00123456789	'0.001234568'@65
		1.234E-06	'0.000001234'@65
		1.23456789E-06	'1.23457E-06'@65
		1.2E-07	'0.00000012'@59.5
		1.23456789E-07	'1.23457E-07'@65
		5E-10	'5E-10'@32
		4.9E-10	'4.9E-10'@43
		5.1E-10	'5.1E-10'@43
		1E-10	'1E-10'@32
		1.23456789E-10	'1.23457E-10'@65
		1E-100	'1E-100'@37.5
		-1.5E-300	'-1.5E-300'@52
		0.000012	'0.000012'@48.5
		0.0000123	'0.0000123'@54
		0.00001234	'0.00001234'@59.5
		0.000012345	'0.000012345'@65
		0.0000012	'0.0000012'@54
		0.00000123	'0.00000123'@59.5
		0.000001234	'0.000001234'@65
		0.0000001234	'1.234E-07'@54
		123.456789012345	'123.456789'@59.5
		0.1234567891234	'0.123456789'@65
		45306.4375	'45306.4375'@59.5
		1.1	'1.1'@21
		10.5	'10.5'@26.5
		100.25	'100.25'@37.5
		-0.5	'-0.5'@24.5
		""";

	/// <summary>Each code, and the text and width of each of <see cref="Values"/> in it: <c>code 'text'@points ...</c>.</summary>
	private const string Codes = """
		0	'0'@10 '1'@10 '-1'@13.5 '1'@10 '1235'@26.5 '-1235'@30 '1234568'@43 '0'@10 '123456789012345'@87.5 '0'@10 '0'@10 '1000'@26.5 '0'@10 '100000000000000000000'@121.5 '-100000000000000000000'@125
		0.00	'0.00'@24 '1.00'@24 '-1.00'@27.5 '0.50'@24 '1234.57'@40.5 '-1234.57'@44 '1234567.89'@57 '0.00'@24 '123456789012345.00'@102 '0.00'@24 '0.00'@24 '1000.00'@40.5 '0.13'@24 '100000000000000000000.00'@135.5 '-100000000000000000000.00'@139.5
		#,##0	'0'@10 '1'@10 '-1'@13.5 '1'@10 '1,235'@29 '-1,235'@32.5 '1,234,568'@48 '0'@10 '123,456,789,012,345'@98 '0'@10 '0'@10 '1,000'@29 '0'@10 '100,000,000,000,000,000,000'@136.5 '-100,000,000,000,000,000,000'@140.5
		#,##0.00	'0.00'@24 '1.00'@24 '-1.00'@27.5 '0.50'@24 '1,234.57'@43 '-1,234.57'@46.5 '1,234,567.89'@62 '0.00'@24 '123,456,789,012,345.00'@112 '0.00'@24 '0.00'@24 '1,000.00'@43 '0.13'@24 '100,000,000,000,000,000,000.00'@151 '-100,000,000,000,000,000,000.00'@154.5
		0%	'0%'@18 '100%'@29 '-100%'@32.5 '50%'@23.5 '123457%'@45.5 '-123457%'@49 '123456789%'@62 '0%'@18 '12345678901234500%'@107 '0%'@18 '0%'@18 '100000%'@45.5 '13%'@23.5 '10000000000000000000000%'@141 '-10000000000000000000000%'@144.5
		0.00%	'0.00%'@32 '100.00%'@43 '-100.00%'@46.5 '50.00%'@37.5 '123456.78%'@59.5 '-123456.78%'@63 '123456789.10%'@76.5 '-0.10%'@35.5 '12345678901234500.00%'@121.5 '0.00%'@32 '0.01%'@32 '99999.50%'@54 '12.50%'@37.5 '10000000000000000000000.00%'@155 '-10000000000000000000000.00%'@158.5
		0.0 %	'0.0 %'@29 '100.0 %'@40 '-100.0 %'@43.5 '50.0 %'@34.5 '123456.8 %'@56.5 '-123456.8 %'@60 '123456789.1 %'@73.5 '-0.1 %'@32.5 '12345678901234500.0 %'@118.5 '0.0 %'@29 '0.0 %'@29 '99999.5 %'@51 '12.5 %'@34.5 '10000000000000000000000.0 %'@152 '-10000000000000000000000.0 %'@155.5
		0.00E+00	'0.00E+00'@46 '1.00E+00'@46 '-1.00E+00'@49.5 '5.00E-01'@44 '1.23E+03'@46 '-1.23E+03'@49.5 '1.23E+06'@46 '-1.00E-03'@47.5 '1.23E+14'@46 '1.00E-07'@44 '1.23E-04'@44 '1.00E+03'@46 '1.25E-01'@44 '1.00E+20'@46 '-1.00E+20'@49.5
		0.00E-00	'0.00E00'@40.5 '1.00E00'@40.5 '-1.00E00'@44 '5.00E-01'@44 '1.23E03'@40.5 '-1.23E03'@44 '1.23E06'@40.5 '-1.00E-03'@47.5 '1.23E14'@40.5 '1.00E-07'@44 '1.23E-04'@44 '1.00E03'@40.5 '1.25E-01'@44 '1.00E20'@40.5 '-1.00E20'@44
		0.00E+000	'0.00E+000'@51.5 '1.00E+000'@51.5 '-1.00E+000'@55 '5.00E-001'@49.5 '1.23E+003'@51.5 '-1.23E+003'@55 '1.23E+006'@51.5 '-1.00E-003'@53 '1.23E+014'@51.5 '1.00E-007'@49.5 '1.23E-004'@49.5 '1.00E+003'@51.5 '1.25E-001'@49.5 '1.00E+020'@51.5 '-1.00E+020'@55
		00.0E+00	'00.0E+00'@46 '01.0E+00'@46 '-01.0E+00'@49.5 '50.0E-02'@44 '12.3E+02'@46 '-12.3E+02'@49.5 '01.2E+06'@46 '-10.0E-04'@47.5 '01.2E+14'@46 '10.0E-08'@44 '01.2E-04'@44 '10.0E+02'@46 '12.5E-02'@44 '01.0E+20'@46 '-01.0E+20'@49.5
		##0.0E+0	'000.0E+0'@46 '1.0E+0'@35 '-1.0E+0'@38.5 '500.0E-3'@44 '1.2E+3'@35 '-1.2E+3'@38.5 '1.2E+6'@35 '-1.0E-3'@36.5 '123.5E+12'@51.5 '100.0E-9'@44 '123.4E-6'@44 '1.0E+3'@35 '125.0E-3'@44 '100.0E+18'@51.5 '-100.0E+18'@55
		#,##0,	'0'@10 '0'@10 '0'@10 '0'@10 '1'@10 '-1'@13.5 '1,235'@29 '0'@10 '123,456,789,012'@78.5 '0'@10 '0'@10 '1'@10 '0'@10 '100,000,000,000,000,000'@117.5 '-100,000,000,000,000,000'@121
		#,##0.0,,"M"	'0.0M'@28 '0.0M'@28 '0.0M'@28 '0.0M'@28 '0.0M'@28 '0.0M'@28 '1.2M'@28 '0.0M'@28 '123,456,789.0M'@77.5 '0.0M'@28 '0.0M'@28 '0.0M'@28 '0.0M'@28 '100,000,000,000,000.0M'@116.5 '-100,000,000,000,000.0M'@120
		$#,##0.00	'$0.00'@29.5 '$1.00'@29.5 '-$1.00'@33 '$0.50'@29.5 '$1,234.57'@48.5 '-$1,234.57'@52 '$1,234,567.89'@67.5 '$0.00'@29.5 '$123,456,789,012,345.00'@118 '$0.00'@29.5 '$0.00'@29.5 '$1,000.00'@48.5 '$0.13'@29.5 '$100,000,000,000,000,000,000.00'@156.5 '-$100,000,000,000,000,000,000.00'@160
		[$€-407] #,##0.00	'€ 0.00'@32 '€ 1.00'@32 '-€ 1.00'@35.5 '€ 0.50'@32 '€ 1,234.57'@51 '-€ 1,234.57'@54.5 '€ 1,234,567.89'@70 '€ 0.00'@32 '€ 123,456,789,012,345.00'@120.5 '€ 0.00'@32 '€ 0.00'@32 '€ 1,000.00'@51 '€ 0.13'@32 '€ 100,000,000,000,000,000,000.00'@159 '-€ 100,000,000,000,000,000,000.00'@163
		#,##0.00 [$€-407]	'0.00 €'@32 '1.00 €'@32 '-1.00 €'@35.5 '0.50 €'@32 '1,234.57 €'@51 '-1,234.57 €'@54.5 '1,234,567.89 €'@70 '0.00 €'@32 '123,456,789,012,345.00 €'@120.5 '0.00 €'@32 '0.00 €'@32 '1,000.00 €'@51 '0.13 €'@32 '100,000,000,000,000,000,000.00 €'@159 '-100,000,000,000,000,000,000.00 €'@163
		[$$-409]#,##0.00	'$0.00'@29.5 '$1.00'@29.5 '-$1.00'@33 '$0.50'@29.5 '$1,234.57'@48.5 '-$1,234.57'@52 '$1,234,567.89'@67.5 '$0.00'@29.5 '$123,456,789,012,345.00'@118 '$0.00'@29.5 '$0.00'@29.5 '$1,000.00'@48.5 '$0.13'@29.5 '$100,000,000,000,000,000,000.00'@156.5 '-$100,000,000,000,000,000,000.00'@160
		0.0;(0.0)	'0.0'@18.5 '1.0'@18.5 '(1.0)'@25.5 '0.5'@18.5 '1234.6'@35 '(1234.6)'@42 '1234567.9'@51.5 '(0.0)'@25.5 '123456789012345.0'@96.5 '0.0'@18.5 '0.0'@18.5 '1000.0'@35 '0.1'@18.5 '100000000000000000000.0'@130 '(100000000000000000000.0)'@137
		#,##0;[Red]-#,##0	'0'@10 '1'@10 '-1'@13.5 '1'@10 '1,235'@29 '-1,235'@32.5 '1,234,568'@48 '-0'@13.5 '123,456,789,012,345'@98 '0'@10 '0'@10 '1,000'@29 '0'@10 '100,000,000,000,000,000,000'@136.5 '-100,000,000,000,000,000,000'@140.5
		0.00;-0.00;"zero"	'zero'@24.5 '1.00'@24 '-1.00'@27.5 '0.50'@24 '1234.57'@40.5 '-1234.57'@44 '1234567.89'@57 '-0.00'@27.5 '123456789012345.00'@102 '0.00'@24 '0.00'@24 '1000.00'@40.5 '0.13'@24 '100000000000000000000.00'@135.5 '-100000000000000000000.00'@139.5
		"Total: "0	'Total: 0'@38.5 'Total: 1'@38.5 '-Total: 1'@42 'Total: 1'@38.5 'Total: 1235'@55 '-Total: 1235'@58.5 'Total: 1234568'@72 'Total: 0'@38.5 'Total: 123456789012345'@117 'Total: 0'@38.5 'Total: 0'@38.5 'Total: 1000'@55 'Total: 0'@38.5 'Total: 100000000000000000000'@150.5 '-Total: 100000000000000000000'@154
		0 "units"	'0 units'@35 '1 units'@35 '-1 units'@38.5 '1 units'@35 '1235 units'@51.5 '-1235 units'@55 '1234568 units'@68 '0 units'@35 '123456789012345 units'@113 '0 units'@35 '0 units'@35 '1000 units'@51.5 '0 units'@35 '100000000000000000000 units'@147 '-100000000000000000000 units'@150.5
		_(* #,##0.00_);_(* (#,##0.00);_(* "-"??_);_(@_)	'    -   '@26 '  1.00 '@31 ' (1.00)'@34.5 '  0.50 '@31 '  1,234.57 '@50 ' (1,234.57)'@53.5 '  1,234,567.89 '@69 ' (0.00)'@34.5 '  123,456,789,012,345.00 '@119.5 '  0.00 '@31 '  0.00 '@31 '  1,000.00 '@50 '  0.13 '@31 '   100,000,000,000,000,000,000.00 '@158 '  (100,000,000,000,000,000,000.00)'@162
		_($* #,##0.00_);_($* (#,##0.00);_($* "-"??_);_(@_)	' $   -   '@31.5 ' $ 1.00 '@36.5 ' $(1.00)'@40 ' $ 0.50 '@36.5 ' $ 1,234.57 '@55.5 ' $(1,234.57)'@59 ' $ 1,234,567.89 '@75 ' $(0.00)'@40 ' $ 123,456,789,012,345.00 '@125 ' $ 0.00 '@36.5 ' $ 0.00 '@36.5 ' $ 1,000.00 '@55.5 ' $ 0.13 '@36.5 ' $  100,000,000,000,000,000,000.00 '@164 ' $ (100,000,000,000,000,000,000.00)'@167.5
		# ?/?	'0    '@28 '1    '@28 '-1    '@31.5 ' 1/2'@22.5 '1234 4/7'@44.5 '-1234 4/7'@48 '1234567 8/9'@61 '-0    '@31.5 '123456789012345    '@106 '0    '@28 '0    '@28 '1000    '@44.5 ' 1/8'@22.5 '100000000000000000000    '@140 '-100000000000000000000    '@143.5
		# ??/??	'0      '@39 '1      '@39 '-1      '@42.5 '  1/2 '@33.5 '1234 46/81'@55.5 '-1234 46/81'@59 '1234567 49/55'@72.5 '-0      '@42.5 '123456789012345      '@117.5 '0      '@39 '0      '@39 '1000      '@55.5 '  1/8 '@33.5 '100000000000000000000      '@151 '-100000000000000000000      '@154.5
		[>=1000]#,##0;0.00	'0.00'@24 '1.00'@24 '-1.00'@27.5 '0.50'@24 '1,235'@29 '-1234.57'@44 '1,234,568'@48 '0.00'@24 '123,456,789,012,345'@98 '0.00'@24 '0.00'@24 '1000.00'@40.5 '0.13'@24 '100,000,000,000,000,000,000'@136.5 '-100000000000000000000.00'@139.5
		[DBNum1]0	'0'@10 '1'@10 '-1'@13.5 '1'@10 '1235'@26.5 '-1235'@30 '1234568'@43 '0'@10 '123456789012345'@87.5 '0'@10 '0'@10 '1000'@26.5 '0'@10 '100000000000000000000'@121.5 '-100000000000000000000'@125
		000000	'000000'@37.5 '000001'@37.5 '-000001'@41 '000001'@37.5 '001235'@37.5 '-001235'@41 '1234568'@43 '000000'@37.5 '123456789012345'@87.5 '000000'@37.5 '000000'@37.5 '001000'@37.5 '000000'@37.5 '100000000000000000000'@121.5 '-100000000000000000000'@125
		00000-0000	'00000-0000'@57.5 '00000-0001'@57.5 '-00000-0001'@61 '00000-0001'@57.5 '00000-1235'@57.5 '-00000-1235'@61 '00123-4568'@57.5 '00000-0000'@57.5 '12345678901-2345'@91 '00000-0000'@57.5 '00000-0000'@57.5 '00000-1000'@57.5 '00000-0000'@57.5 '10000000000000000-0000'@125 '-10000000000000000-0000'@128.5
		#,##0.00_);(#,##0.00)	'0.00 '@27.5 '1.00 '@27.5 '(1.00)'@31 '0.50 '@27.5 '1,234.57 '@46.5 '(1,234.57)'@50 '1,234,567.89 '@65.5 '(0.00)'@31 '123,456,789,012,345.00 '@116 '0.00 '@27.5 '0.00 '@27.5 '1,000.00 '@46.5 '0.13 '@27.5 '100,000,000,000,000,000,000.00 '@154.5 '(100,000,000,000,000,000,000.00)'@158
		0.0#####	'0.0'@18.5 '1.0'@18.5 '-1.0'@22 '0.5'@18.5 '1234.5678'@51.5 '-1234.5678'@55 '1234567.891'@62.5 '-0.001'@33 '123456789012345.0'@96.5 '0.0'@18.5 '0.000123'@46 '999.995'@40.5 '0.125'@29.5 '100000000000000000000.0'@130 '-100000000000000000000.0'@133.5
		#.##	'.'@7.5 '1.'@13 '-1.'@16.5 '.5'@13 '1234.57'@40.5 '-1234.57'@44 '1234567.89'@57 '.'@7.5 '123456789012345.'@90.5 '.'@7.5 '.'@7.5 '1000.'@29.5 '.13'@18.5 '100000000000000000000.'@124.5 '-100000000000000000000.'@128
		?.??	' .  '@24 '1.  '@24 '-1.  '@27.5 ' .5 '@24 '1234.57'@40.5 '-1234.57'@44 '1234567.89'@57 ' .  '@24 '123456789012345.  '@102 ' .  '@24 ' .  '@24 '1000.  '@40.5 ' .13'@24 '100000000000000000000.  '@135.5 '-100000000000000000000.  '@139.5
		0.000??	'0.000  '@40.5 '1.000  '@40.5 '-1.000  '@44 '0.500  '@40.5 '1234.5678 '@57 '-1234.5678 '@60.5 '1234567.891  '@74 '-0.001  '@44 '123456789012345.000  '@119 '0.000  '@40.5 '0.00012'@40.5 '999.995  '@51.5 '0.125  '@40.5 '100000000000000000000.000  '@152.5 '-100000000000000000000.000  '@156
		@	'0'@10 '1'@10 '-1'@13.5 '1'@18.5 '1234.568'@51.5 '-1234.568'@55 '1234567.89'@62.5 '0'@33 '1.2346E+14'@62.5 '1E-07'@51.5 '0.000123'@51.5 '1000'@40.5 '0.13'@29.5 '1E+20'@32 '-1E+20'@35.5
		General	'0'@10 '1'@10 '-1'@13.5 '0.5'@21 '1234.5678'@54 '-1234.5678'@57.5 '1234567.891'@65 '-0.001'@35.5 '1.23457E+14'@65 '0.0000001'@54 '0.0001234'@54 '999.995'@43 '0.125'@32 '1E+20'@32 '-1E+20'@35.5
		"$"General	'$0'@15.5 '$1'@15.5 '-$1'@19 '$1'@24 '$1234.568'@57 '-$1234.568'@60.5 '$1234567.89'@68 '$0'@38.5 '$1.2346E+14'@68 '$1E-07'@57 '$0.000123'@57 '$1000'@46 '$0.13'@35 '$1E+20'@37.5 '-$1E+20'@41
		[Blue]0.0	'0.0'@18.5 '1.0'@18.5 '-1.0'@22 '0.5'@18.5 '1234.6'@35 '-1234.6'@38.5 '1234567.9'@51.5 '0.0'@18.5 '123456789012345.0'@96.5 '0.0'@18.5 '0.0'@18.5 '1000.0'@35 '0.1'@18.5 '100000000000000000000.0'@130 '-100000000000000000000.0'@133.5
		#	''@48 '1'@10 '-1'@13.5 '1'@10 '1235'@26.5 '-1235'@30 '1234568'@43 ''@48 '123456789012345'@87.5 ''@48 ''@48 '1000'@26.5 ''@48 '100000000000000000000'@121.5 '-100000000000000000000'@125
		0.0\x	'0.0x'@23.5 '1.0x'@23.5 '-1.0x'@27 '0.5x'@23.5 '1234.6x'@40 '-1234.6x'@43.5 '1234567.9x'@56.5 '0.0x'@23.5 '123456789012345.0x'@101.5 '0.0x'@23.5 '0.0x'@23.5 '1000.0x'@40 '0.1x'@23.5 '100000000000000000000.0x'@135 '-100000000000000000000.0x'@139
		#,##0.00\ "kr"	'0.00 kr'@35.5 '1.00 kr'@35.5 '-1.00 kr'@39 '0.50 kr'@35.5 '1,234.57 kr'@54.5 '-1,234.57 kr'@58 '1,234,567.89 kr'@74 '0.00 kr'@35.5 '123,456,789,012,345.00 kr'@124 '0.00 kr'@35.5 '0.00 kr'@35.5 '1,000.00 kr'@54.5 '0.13 kr'@35.5 '100,000,000,000,000,000,000.00 kr'@163 '-100,000,000,000,000,000,000.00 kr'@166.5
		#,###	''@48 '1'@10 '-1'@13.5 '1'@10 '1,235'@29 '-1,235'@32.5 '1,234,568'@48 ''@48 '123,456,789,012,345'@98 ''@48 ''@48 '1,000'@29 ''@48 '100,000,000,000,000,000,000'@136.5 '-100,000,000,000,000,000,000'@140.5
		#,	''@48 ''@48 ''@48 ''@48 '1'@10 '-1'@13.5 '1235'@26.5 ''@48 '123456789012'@71 ''@48 ''@48 '1'@10 ''@48 '100000000000000000'@104.5 '-100000000000000000'@108
		"("0")"	'(0)'@17 '(1)'@17 '-(1)'@20.5 '(1)'@17 '(1235)'@33.5 '-(1235)'@37 '(1234568)'@50 '(0)'@17 '(123456789012345)'@95 '(0)'@17 '(0)'@17 '(1000)'@33.5 '(0)'@17 '(100000000000000000000)'@128.5 '-(100000000000000000000)'@132
		0_)	'0 '@13.5 '1 '@13.5 '-1 '@17 '1 '@13.5 '1235 '@30 '-1235 '@33.5 '1234568 '@46.5 '0 '@13.5 '123456789012345 '@91 '0 '@13.5 '0 '@13.5 '1000 '@30 '0 '@13.5 '100000000000000000000 '@125 '-100000000000000000000 '@128.5
		#,##0.00;;	''@48 '1.00'@24 ''@48 '0.50'@24 '1,234.57'@43 ''@48 '1,234,567.89'@62 ''@48 '123,456,789,012,345.00'@112 '0.00'@24 '0.00'@24 '1,000.00'@43 '0.13'@24 '100,000,000,000,000,000,000.00'@151 ''@48
		0;-0;;@	''@48 '1'@10 '-1'@13.5 '1'@10 '1235'@26.5 '-1235'@30 '1234568'@43 '-0'@13.5 '123456789012345'@87.5 '0'@10 '0'@10 '1000'@26.5 '0'@10 '100000000000000000000'@121.5 '-100000000000000000000'@125
		""";

	/// <summary>
	/// Each date code, the width of a column of the 14 <see cref="Dates"/> in it, and each date's text:
	/// <c>code points 'text' ...</c>.
	/// </summary>
	private const string DateTexts = """
		dddd, mmmm dd, yyyy	156	'Sunday, January 28, 2024' 'Wednesday, February 28, 2024' 'Thursday, March 28, 2024' 'Sunday, April 28, 2024' 'Tuesday, May 28, 2024' 'Friday, June 28, 2024' 'Sunday, July 28, 2024' 'Wednesday, August 28, 2024' 'Saturday, September 28, 2024' 'Monday, October 28, 2024' 'Thursday, November 28, 2024' 'Saturday, December 28, 2024' 'Wednesday, September 25, 2024' 'Saturday, May 04, 2024'
		mmm d, yyyy	65	'Jan 28, 2024' 'Feb 28, 2024' 'Mar 28, 2024' 'Apr 28, 2024' 'May 28, 2024' 'Jun 28, 2024' 'Jul 28, 2024' 'Aug 28, 2024' 'Sep 28, 2024' 'Oct 28, 2024' 'Nov 28, 2024' 'Dec 28, 2024' 'Sep 25, 2024' 'May 4, 2024'
		d-mmm-yy	53.5	'28-Jan-24' '28-Feb-24' '28-Mar-24' '28-Apr-24' '28-May-24' '28-Jun-24' '28-Jul-24' '28-Aug-24' '28-Sep-24' '28-Oct-24' '28-Nov-24' '28-Dec-24' '25-Sep-24' '4-May-24'
		mmmm	54.5	'January' 'February' 'March' 'April' 'May' 'June' 'July' 'August' 'September' 'October' 'November' 'December' 'September' 'May'
		mmmmm	14	'J' 'F' 'M' 'A' 'M' 'J' 'J' 'A' 'S' 'O' 'N' 'D' 'S' 'M'
		mmm	24.5	'Jan' 'Feb' 'Mar' 'Apr' 'May' 'Jun' 'Jul' 'Aug' 'Sep' 'Oct' 'Nov' 'Dec' 'Sep' 'May'
		ddd	26	'Sun' 'Wed' 'Thu' 'Sun' 'Tue' 'Fri' 'Sun' 'Wed' 'Sat' 'Mon' 'Thu' 'Sat' 'Wed' 'Sat'
		dddd	58.5	'Sunday' 'Wednesday' 'Thursday' 'Sunday' 'Tuesday' 'Friday' 'Sunday' 'Wednesday' 'Saturday' 'Monday' 'Thursday' 'Saturday' 'Wednesday' 'Saturday'
		h:mm AM/PM	47	'9:05 AM' '9:05 AM' '9:05 AM' '9:05 AM' '9:05 AM' '9:05 AM' '9:05 AM' '9:05 AM' '9:05 AM' '9:05 AM' '9:05 AM' '9:05 AM' '11:59 PM' '11:59 PM'
		h:mm:ss A/P	51.5	'9:05:07 A' '9:05:07 A' '9:05:07 A' '9:05:07 A' '9:05:07 A' '9:05:07 A' '9:05:07 A' '9:05:07 A' '9:05:07 A' '9:05:07 A' '9:05:07 A' '9:05:07 A' '11:59:59 P' '11:59:59 P'
		yyyy-mm-dd hh:mm:ss.000	118	'2024-01-28 09:05:07.250' '2024-02-28 09:05:07.250' '2024-03-28 09:05:07.250' '2024-04-28 09:05:07.250' '2024-05-28 09:05:07.250' '2024-06-28 09:05:07.250' '2024-07-28 09:05:07.250' '2024-08-28 09:05:07.250' '2024-09-28 09:05:07.250' '2024-10-28 09:05:07.250' '2024-11-28 09:05:07.250' '2024-12-28 09:05:07.250' '2024-09-25 23:59:59.000' '2024-05-04 23:59:59.000'
		dd/mm/yyyy hh:mm	85.5	'28/01/2024 09:05' '28/02/2024 09:05' '28/03/2024 09:05' '28/04/2024 09:05' '28/05/2024 09:05' '28/06/2024 09:05' '28/07/2024 09:05' '28/08/2024 09:05' '28/09/2024 09:05' '28/10/2024 09:05' '28/11/2024 09:05' '28/12/2024 09:05' '25/09/2024 23:59' '04/05/2024 23:59'
		yy	15.5	'24' '24' '24' '24' '24' '24' '24' '24' '24' '24' '24' '24' '24' '24'
		yyyy	26.5	'2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024'
		m/d/yyyy h:mm	80	'1/28/2024 9:05' '2/28/2024 9:05' '3/28/2024 9:05' '4/28/2024 9:05' '5/28/2024 9:05' '6/28/2024 9:05' '7/28/2024 9:05' '8/28/2024 9:05' '9/28/2024 9:05' '10/28/2024 9:05' '11/28/2024 9:05' '12/28/2024 9:05' '9/25/2024 23:59' '5/4/2024 23:59'
		d	15.5	'28' '28' '28' '28' '28' '28' '28' '28' '28' '28' '28' '28' '25' '4'
		dd	15.5	'28' '28' '28' '28' '28' '28' '28' '28' '28' '28' '28' '28' '25' '04'
		m	15.5	'1' '2' '3' '4' '5' '6' '7' '8' '9' '10' '11' '12' '9' '5'
		h	15.5	'9' '9' '9' '9' '9' '9' '9' '9' '9' '9' '9' '9' '23' '23'
		hh:mm	29.5	'09:05' '09:05' '09:05' '09:05' '09:05' '09:05' '09:05' '09:05' '09:05' '09:05' '09:05' '09:05' '23:59' '23:59'
		mm:ss.0	38	'05:07.2' '05:07.2' '05:07.2' '05:07.2' '05:07.2' '05:07.2' '05:07.2' '05:07.2' '05:07.2' '05:07.2' '05:07.2' '05:07.2' '59:59.0' '59:59.0'
		[$-407]dddd, d. mmmm yyyy	153.5	'Sonntag, 28. Januar 2024' 'Mittwoch, 28. Februar 2024' 'Donnerstag, 28. März 2024' 'Sonntag, 28. April 2024' 'Dienstag, 28. Mai 2024' 'Freitag, 28. Juni 2024' 'Sonntag, 28. Juli 2024' 'Mittwoch, 28. August 2024' 'Samstag, 28. September 2024' 'Montag, 28. Oktober 2024' 'Donnerstag, 28. November 2024' 'Samstag, 28. Dezember 2024' 'Mittwoch, 25. September 2024' 'Samstag, 4. Mai 2024'
		[$-40C]dddd d mmmm yyyy	137	'dimanche 28 janvier 2024' 'mercredi 28 février 2024' 'jeudi 28 mars 2024' 'dimanche 28 avril 2024' 'mardi 28 mai 2024' 'vendredi 28 juin 2024' 'dimanche 28 juillet 2024' 'mercredi 28 août 2024' 'samedi 28 septembre 2024' 'lundi 28 octobre 2024' 'jeudi 28 novembre 2024' 'samedi 28 décembre 2024' 'mercredi 25 septembre 2024' 'samedi 4 mai 2024'
		e	26.5	'2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024' '2024'
		dddd mmmm	112	'Sunday January' 'Wednesday February' 'Thursday March' 'Sunday April' 'Tuesday May' 'Friday June' 'Sunday July' 'Wednesday August' 'Saturday September' 'Monday October' 'Thursday November' 'Saturday December' 'Wednesday September' 'Saturday May'
		yyyy"年"m"月"d"日"	82	'2024年1月28日' '2024年2月28日' '2024年3月28日' '2024年4月28日' '2024年5月28日' '2024年6月28日' '2024年7月28日' '2024年8月28日' '2024年9月28日' '2024年10月28日' '2024年11月28日' '2024年12月28日' '2024年9月25日' '2024年5月4日'
		d mmmm yyyy	93.5	'28 January 2024' '28 February 2024' '28 March 2024' '28 April 2024' '28 May 2024' '28 June 2024' '28 July 2024' '28 August 2024' '28 September 2024' '28 October 2024' '28 November 2024' '28 December 2024' '25 September 2024' '4 May 2024'
		ddd d mmm	59.5	'Sun 28 Jan' 'Wed 28 Feb' 'Thu 28 Mar' 'Sun 28 Apr' 'Tue 28 May' 'Fri 28 Jun' 'Sun 28 Jul' 'Wed 28 Aug' 'Sat 28 Sep' 'Mon 28 Oct' 'Thu 28 Nov' 'Sat 28 Dec' 'Wed 25 Sep' 'Sat 4 May'
		mmmm d, yyyy h:mm:ss AM/PM	156	'January 28, 2024 9:05:07 AM' 'February 28, 2024 9:05:07 AM' 'March 28, 2024 9:05:07 AM' 'April 28, 2024 9:05:07 AM' 'May 28, 2024 9:05:07 AM' 'June 28, 2024 9:05:07 AM' 'July 28, 2024 9:05:07 AM' 'August 28, 2024 9:05:07 AM' 'September 28, 2024 9:05:07 AM' 'October 28, 2024 9:05:07 AM' 'November 28, 2024 9:05:07 AM' 'December 28, 2024 9:05:07 AM' 'September 25, 2024 11:59:59 PM' 'May 4, 2024 11:59:59 PM'
		""";

	/// <summary>Each elapsed time code, and the text and width of each of <see cref="Durations"/> in it.</summary>
	private const string Elapsed = """
		[h]:mm:ss	'12:00:00'@43.5 '30:00:00'@43.5 '258:00:00'@49 '2400:00:09'@54.5 '0:01:00'@38 '100000:00:00'@65.5
		[h]:mm	'12:00'@29.5 '30:00'@29.5 '258:00'@35 '2400:00'@40.5 '0:01'@24 '100000:00'@51.5
		[mm]:ss	'720:00'@35 '1800:00'@40.5 '15480:00'@46 '144000:09'@51.5 '01:00'@29.5 '6000000:00'@57
		[ss].00	'43200.00'@46 '108000.00'@51.5 '928800.00'@51.5 '8640008.64'@57 '60.00'@29.5 '360000000.00'@68
		[hh]:mm:ss	'12:00:00'@43.5 '30:00:00'@43.5 '258:00:00'@49 '2400:00:09'@54.5 '00:01:00'@43.5 '100000:00:00'@65.5
		[h]:mm:ss.000	'12:00:00.000'@63 '30:00:00.000'@63 '258:00:00.000'@68.5 '2400:00:08.640'@74.5 '0:01:00.000'@57.5 '100000:00:00.000'@85.5
		""";

	/// <summary>The values of each line of <see cref="Codes"/>, in order.</summary>
	private static readonly double[] Values = [0, 1, -1, 0.5, 1234.5678, -1234.5678, 1234567.891, -0.001, 123456789012345, 1E-07, 0.0001234, 999.995, 0.125, 1E+20, -1E+20];

	/// <summary>The durations of each line of <see cref="Elapsed"/>, in days, in order.</summary>
	private static readonly double[] Durations = [0.5, 1.25, 10.75, 100.0001, 0.000694444444444444, 4166.66666666667];

	/// <summary>
	/// The codes whose text is estimated, not Excel's: fills, which repeat as the column has room; fractions; conditions;
	/// and <c>@</c> alone and General among literals, whose numbers Excel shows to the column's width at the time.
	/// </summary>
	private static readonly HashSet<string> Estimated =
	[
		"_(* #,##0.00_);_(* (#,##0.00);_(* \"-\"??_);_(@_)", "_($* #,##0.00_);_($* (#,##0.00);_($* \"-\"??_);_(@_)",
		"# ?/?", "# ??/??", "[>=1000]#,##0;0.00", "@", "\"$\"General",
	];

	/// <summary>The lines of <see cref="General"/>: each number, its text and its width.</summary>
	public static TheoryData<string, string, double> GeneralLines
	{
		get
		{
			var data = new TheoryData<string, string, double>();
			foreach (var line in Lines(General))
			{
				var (text, points) = Parse(line.Entries).Single();
				data.Add(line.Key, text, points);
			}

			return data;
		}
	}

	/// <summary>The codes of <see cref="Codes"/>.</summary>
	public static TheoryData<string> CodeLines => Keys(Codes);

	/// <summary>The codes of <see cref="DateTexts"/>.</summary>
	public static TheoryData<string> DateLines => Keys(DateTexts);

	/// <summary>The codes of <see cref="Elapsed"/>.</summary>
	public static TheoryData<string> ElapsedLines => Keys(Elapsed);

	/// <summary>
	/// The dates of each line of <see cref="DateTexts"/>, in order: the 28th of each month of 2024 at 09:05:07.250, then
	/// 2024-09-25 and 2024-05-04 at 23:59:59.
	/// </summary>
	private static IEnumerable<DateTime> Dates =>
		Enumerable.Range(1, 12).Select(m => new DateTime(2024, m, 28, 9, 5, 7, 250)).Concat([new DateTime(2024, 9, 25, 23, 59, 59), new DateTime(2024, 5, 4, 23, 59, 59)]);

	[Theory]
	[MemberData(nameof(GeneralLines))]
	public void ShowsNumbersInGeneralAsExcel(string value, string text, double points)
	{
		var number = double.Parse(value, CultureInfo.InvariantCulture);

		Assert.Equal(text, NumberFormatWidth.General(number));
		Assert.InRange(Points(NumberFormatWidth.GeneralPixels(number)), points - Rounding, points + 1.5);
	}

	[Theory]
	[MemberData(nameof(CodeLines))]
	public void ShowsNumbersInCodesAsExcel(string code)
	{
		var width = new NumberFormatWidth(code);
		var texts = Parse(Lines(Codes).Single(l => l.Key == code).Entries);

		Assert.Equal(Values.Length, texts.Count);
		for (var i = 0; i < Values.Length; i++)
		{
			if (!Estimated.Contains(code))
			{
				Assert.Equal(texts[i].Text, width.Number(Values[i], widest: false));
			}

			// A value Excel shows nothing for leaves its column as it was.
			if (texts[i].Text.Length > 0)
			{
				var points = Points(width.NumberPixels(Values[i]));
				Assert.True(points >= texts[i].Points - Rounding, $"{Values[i]} in {code}: {points} points, Excel's {texts[i].Points}.");
			}
		}
	}

	[Theory]
	[MemberData(nameof(DateLines))]
	public void ShowsDatesInCodesAsExcel(string code)
	{
		var width = new NumberFormatWidth(code);
		var parts = Lines(DateTexts).Single(l => l.Key == code).Entries.Split('\t');
		var texts = Regex.Matches(parts[1], "'([^']*)'").Cast<Match>().Select(m => m.Groups[1].Value);

		Assert.Equal(texts, Dates.Select(d => width.Number(d.ToOADate(), widest: false)));
		Assert.True(Points(Dates.Max(d => width.NumberPixels(d.ToOADate()))) >= double.Parse(parts[0], CultureInfo.InvariantCulture) - Rounding);
	}

	[Theory]
	[MemberData(nameof(ElapsedLines))]
	public void ShowsElapsedTimeAsExcel(string code)
	{
		var width = new NumberFormatWidth(code);
		var texts = Parse(Lines(Elapsed).Single(l => l.Key == code).Entries);

		Assert.Equal(texts.Select(t => t.Text), Durations.Select(d => width.Number(d, widest: false)));
		for (var i = 0; i < Durations.Length; i++)
		{
			Assert.True(Points(width.NumberPixels(Durations[i])) >= texts[i].Points - Rounding);
		}
	}

	[Theory]
	// Excel's text and width of "some text" and of TRUE in each code: booleans take the text section too.
	[InlineData("0.00", "some text", 49.5, "TRUE", 28.5)]
	[InlineData("0;-0;;@", "some text", 49.5, "TRUE", 28.5)]
	[InlineData("0;-0;0;\"Note: \"@", "Note: some text", 77.5, "Note: TRUE", 56)]
	[InlineData("\"Note: \"@", "Note: some text", 77.5, "Note: TRUE", 56)]
	[InlineData("@\" end\"", "some text end", 69.5, "TRUE end", 48.5)]
	[InlineData("General", "some text", 49.5, "TRUE", 28.5)]
	public void ShowsTextAndBooleansInTheTextSection(string code, string text, double textPoints, string boolean, double booleanPoints)
	{
		var width = new NumberFormatWidth(code);

		Assert.Equal(text, width.Text("some text"));
		Assert.Equal(boolean, width.Text("TRUE"));
		Assert.InRange(Points(width.TextPixels(ColumnWidths.TextPixels("some text".AsSpan()))), textPoints - Rounding, textPoints + 1);
		Assert.InRange(Points(width.TextPixels(ColumnWidths.TextPixels("TRUE".AsSpan()))), booleanPoints - Rounding, booleanPoints + 1);
	}

	[Fact]
	public void MeasuresTheWidestFormOfDates()
	{
		// Names as their language's widest, and days, months and hours in two digits, whatever the date.
		var january = new DateTime(2024, 1, 1, 1, 0, 0).ToOADate();

		Assert.Equal("1 January 2024 1:00 AM", new NumberFormatWidth("d mmmm yyyy h:mm AM/PM").Number(january, widest: false));
		Assert.Equal("01 September 2024 01:00 AM", new NumberFormatWidth("d mmmm yyyy h:mm AM/PM").Number(january, widest: true));
		Assert.Equal("M", new NumberFormatWidth("mmmmm").Number(january, widest: true));
		Assert.Equal("Wednesday", new NumberFormatWidth("dddd").Number(january, widest: true));
		Assert.Equal("Donnerstag", new NumberFormatWidth("[$-407]dddd").Number(january, widest: true));

		// The wider designator: AM in capitals, p in lower case.
		Assert.Equal("AM", new NumberFormatWidth("AM/PM").Number(january, widest: true));
		Assert.Equal("p", new NumberFormatWidth("a/p").Number(january, widest: true));
	}

	[Fact]
	public void MeasuresDBNumAsEastAsianNumerals()
	{
		// An East Asian locale shows [DBNum1] in numerals (四万五千三百六 for 45306), Excel elsewhere in digits: each digit
		// is measured as a numeral and a unit.
		var width = new NumberFormatWidth("[DBNum1]0");

		Assert.Equal("45307", width.Number(45306.5, widest: false));
		Assert.Equal(10 * ColumnWidths.TextPixels(((char)0x5E74).ToString().AsSpan()), width.NumberPixels(45306.5), 6);
	}

	[Fact]
	public void MeasuresSeparatorsAsTheWidestOfCommonLocales()
	{
		// A period is wider than a comma in Calibri; de-DE groups with periods, en-US with commas.
		var width = new NumberFormatWidth("#,##0.00");

		Assert.Equal("1,234,567.89", width.Number(1234567.891, widest: false));
		Assert.Equal("1.234.567.89", width.Number(1234567.891, widest: true));
	}

	[Theory]
	[InlineData("0.0", -0.04, "0.0")]
	[InlineData("0.0", -0.05, "-0.1")]
	[InlineData("0.0;-0.0", -0.04, "-0.0")]
	[InlineData("0;\"neg\"", -5, "neg")]
	[InlineData("0;-0;", 0, "")]
	[InlineData(".00", 12.345, "12.35")]
	[InlineData("0.00E+00", 999.995, "1.00E+03")]
	[InlineData("##0.0E+0", 999.96, "1.0E+3")]
	[InlineData("000-00", 12345, "123-45")]
	[InlineData("#,##0,,", 2500000000, "2,500")]
	[InlineData("[Red]0%", 0.5, "50%")]
	[InlineData("0.0\"%\"", 0.5, "0.5%")]
	[InlineData("[h]:mm:ss", 1.5, "36:00:00")]
	// Excel's days of the week before 1900-03-01, a day earlier than they were; a date part over elapsed time; aaa and
	// aaaa as the day's names.
	[InlineData("dddd", 1.5, "Sunday")]
	[InlineData("dddd", 61, "Thursday")]
	[InlineData("yyyy-mm-dd [h]:mm", 1.5, "1900-01-01 12:00")]
	[InlineData("aaaa", 45306.5, "Monday")]
	[InlineData("[$-407]aaa", 45306.5, "Mo")]
	public void ModelsSignsRoundingAndPlaceholders(string code, double value, string text)
	{
		Assert.Equal(text, new NumberFormatWidth(code).Number(value, widest: false));
	}

	[Theory]
	// Codes it doesn't model are never narrower than Excel's text: fractions, conditions (their widest section), and
	// [DBNum], whose East Asian numerals aren't modeled (at least General).
	[InlineData("# ??/??", 1234.5678, "1234 46/81")]
	[InlineData("[>=1000]#,##0;0.00", 1234.5678, "1,235")]
	[InlineData("[DBNum1]0", 0.125, "0")]
	[InlineData("[DBNum1]0", 1E+20, "100000000000000000000")]
	public void ErrsWideForCodesItDoesntModel(string code, double value, string excel)
	{
		Assert.True(new NumberFormatWidth(code).NumberPixels(value) >= ColumnWidths.TextPixels(excel.AsSpan()));
	}

	[Fact]
	public void ShowsTheCorpusCodesAsExcel()
	{
		// Phase 4's answers (Files/excel-formats.json): Excel's text of 2024-01-15 12:00 and of a day and a half in each code
		// it takes and FormatCheck passes. Codes whose text is estimated are only never narrower: fractions, conditions,
		// [DBNum], eras, the system's long date ([$-F800]) and General among literals. Fills are neither: Excel's text
		// there repeats them across a column widened for the answers.
		var failures = new List<string>();
		var count = 0;
		foreach (var item in LoadCorpusCodes())
		{
			var code = item.Element("excelCode")!.Value;
			if (FormatCheck.Problem(code) is not null || code.Contains('*'))
			{
				continue;
			}

			var width = new NumberFormatWidth(code);
			var estimated = code.Contains('?') || code.Contains("[>") || code.Contains("[<") || code.Contains("[=") || code.Contains("DBNum") ||
				code.Contains('g') || code.Contains("F800") || (code.Contains("General") && code != "General") || code == "@";
			foreach (var (value, name) in new[] { (45306.5, "text"), (1.5, "durationText") })
			{
				var text = item.Element(name)!.Value;
				var shown = width.Number(value, widest: false);
				if (!estimated && shown != text)
				{
					failures.Add($"{code} {value}: Excel '{text}', '{shown}'");
				}

				if (ColumnWidths.TextPixels(text.AsSpan()) > width.NumberPixels(value) + 0.001)
				{
					failures.Add($"{code} {value}: narrower than Excel's '{text}': '{width.Number(value, widest: true)}'");
				}
			}

			count++;
		}

		Assert.True(count > 150, $"{count} codes");
		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void ShowsNothingForValuesExcelCant()
	{
		Assert.Equal("", new NumberFormatWidth("yyyy-mm-dd").Number(-1, widest: false));
		Assert.Equal("", new NumberFormatWidth("yyyy-mm-dd").Number(2958466, widest: false));
		Assert.Equal("", new NumberFormatWidth("0.00").Number(double.NaN, widest: false));
		Assert.Equal("0", NumberFormatWidth.General(double.PositiveInfinity));
	}

	/// <summary>The codes Excel takes of Phase 4's answers about number formats, read as XML, as <c>NumberFormatsTests</c> reads them.</summary>
	/// <returns>Each code's item.</returns>
	private static IEnumerable<XElement> LoadCorpusCodes()
	{
		var bytes = File.ReadAllBytes(CorpusFiles.PathOf("excel-formats.json"));
		using var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max);

		return [.. XElement.Load(reader).Element("codes")!.Elements().Where(e => e.Element("valid")?.Value == "true")];
	}

	/// <summary>A width in points, at 96 DPI: the text's whole pixels and the padding.</summary>
	/// <param name="pixels">The text's pixels.</param>
	/// <returns>The points.</returns>
	private static double Points(double pixels) => (Math.Ceiling(pixels) + 5) * 0.75;

	/// <summary>A table's lines: each line's key, before its first tab, and its entries.</summary>
	/// <param name="table">The table.</param>
	/// <returns>The lines.</returns>
	private static IEnumerable<(string Key, string Entries)> Lines(string table) =>
		table.Split('\n').Select(l => l.Trim('\r', '\t')).Where(l => l.Length > 0).Select(l => (l.Substring(0, l.IndexOf('\t')), l.Substring(l.IndexOf('\t') + 1)));

	/// <summary>A table's keys.</summary>
	/// <param name="table">The table.</param>
	/// <returns>The keys.</returns>
	private static TheoryData<string> Keys(string table)
	{
		var data = new TheoryData<string>();
		foreach (var line in Lines(table))
		{
			data.Add(line.Key);
		}

		return data;
	}

	/// <summary>Reads <c>'text'@points</c> entries.</summary>
	/// <param name="entries">The entries.</param>
	/// <returns>The texts and widths.</returns>
	private static List<(string Text, double Points)> Parse(string entries) =>
		[.. Regex.Matches(entries, "'([^']*)'@([0-9.]+)").Cast<Match>().Select(m => (m.Groups[1].Value, double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)))];
}

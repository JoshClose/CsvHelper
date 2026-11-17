"""Writes the display set (Phase 8's step 7): the workbooks of Phase 8's probes of how Excel shows values in number
formats, a sheet each, since Excel holds 200 to 250 custom formats a workbook. They are the design's probes and steps 3,
4 and 7's (step 7's halves of seconds, and the truncation of their parts), written as those steps wrote them
(Csv/_bench/results/phase8), less the codes Excel refuses (REFUSED), which it won't open a workbook with.

Every case is a cell C<row> of its sheet: column A names the case (<sheet>:<row>), column B holds its format code as text
(or "id <n>" for a built-in id, which the styles part doesn't define), and column C the value in a style of that code: a
number, a text, a boolean or an error. A sheet's workbook is in the 1904 date system when its name says so (dates1904,
sections1904, limits1904, system1904). The files' bytes repeat: every entry of a workbook has the same time.

Run by the corpus tool's display command: python -I display.py <folder> <cases.json>. It writes <folder>/<sheet>.xlsx and
the cases (file, sheet, row, id, code or built-in id, the value's kind and its stored text, the column's width, the date
system) as JSON.
"""

import json
import os
import sys
import zipfile
from fractions import Fraction

# The codes Excel refuses through COM (the probes' refused*.json): a workbook with one doesn't open.
REFUSED = {
    "0.00e+00", "0:00", "0.00 General", "[Color57]0", "[Color0]0",
    '0;0;[=0]"z"', '0;0;[>5]"z"', '[>100]0;0.00;[=0]"z"', "# ?/100000000",
    "0E+", "0.0E+", "0.00E-", '0E+" x"', "#E+", "0E+;0",
    "d [h] AM/PM", "h 0 m", "[h]:mm AM/PM", "h:mm:ss.0000", "yyyy 0.0", "0.0 yyyy", "yyyy 0", "yyyy #", "yyyy ?",
    "yyyy 0%", "h:mm%", "yyyy E+0", "e+0", "h@m", "@;[$-F800]",
}
# Step 4's length7 codes Excel refuses: a quoted literal of 200 characters, and 28 days' names with quotes between them.
_X200 = '"' + "x" * 200 + '"'
REFUSED |= {'""'.join(["dddd"] * 28) + tail for tail in ('"xy"', '"xyz"', '"xyzw"')}
REFUSED |= {_X200 + "[h]", _X200 + "0", _X200 + "[s];" + _X200 + "[s]"}
# And step 4's length6 code of a quoted literal of 248 characters before [h].
REFUSED.add('"' + "x" * 248 + '"[h]')

MAIN = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
REL = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
PKG = "http://schemas.openxmlformats.org/package/2006/relationships"
CT = "application/vnd.openxmlformats-officedocument.spreadsheetml."
DECL = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\r\n'
BS = chr(92)
EURO = chr(0x20AC)
POUND = chr(0xA3)
YEN = chr(0xA5)
RUPEE = chr(0x20B9)
NEN = chr(0x5E74)
GATSU = chr(0x6708)
NICHI = chr(0x65E5)


def num(x):
    """A number's stored text: the shortest round trip, with an upper-case exponent."""
    if isinstance(x, str):
        return x
    s = repr(float(x))
    if s.endswith(".0"):
        s = s[:-2]
    return s.replace("e", "E")


def N(*values):
    return [("n", num(v)) for v in values]


def S(*values):
    return [("s", v) for v in values]


B = [("b", "1"), ("b", "0")]
E = [("e", v) for v in ("#N/A", "#DIV/0!", "#VALUE!", "#REF!", "#NAME?", "#NUM!", "#NULL!")]

# Sheet name -> (column C width, [(code or ("id", n), [values])]).
SHEETS = {}


def sheet(name, width, cases):
    SHEETS[name] = (width, cases)


# General: significant digits and the switch to exponents, at a wide column.
general_values = N(
    0, "-0", 1, -1, 0.1, 0.1 + 0.2, 1 / 3, 2 / 3, -2 / 3, 10 / 3, 12345678901, -12345678901, 123456789012, -123456789012,
    99999999999, 99999999999.5, 999999999999, 1234567890.5, 12345678.9012345, 0.1234567890123, 0.00001, -0.00001,
    0.000012345, 0.0001234567, 0.0001, 0.001234567891, 1.5e-5, 1e-10, 1.23456789e-10, 1e15, 1.5e15, 1e100, -1e100,
    1.7976931348623157e308, 5e-324, 2.2250738585072014e-308, -1.23456789e20, 100000000000, 10000000000, 1e10, 0.000001,
    1234.5, 123456.789, -0.000012345, 0.99999999999, 0.999999999995, 99999999999.9, 0.000123456789, 1e-5 + 1e-20,
    123456789.123, 1234567890123456789, 0.5, 2.5, 1e21, 1e-7, 9.87654321e-5, 4.35 * 100, 0.1 + 0.7, 1e11 - 0.5,
)
sheet("general", 100, [
    ("General", general_values),
    ("0.00;General", N(-1234.5678, -0.30000000000000004, -123456789012)),
    ('"Total: "General', N(1234.5678, 123456789012, -0.5)),
    ('General" units"', N(1234.5678, -0.000012345)),
    ("[Red]General", N(-0.30000000000000004, 123456789012)),
    ("General;-General", N(-1234.5678, -123456789012)),
    ("General;(General)", N(-1234.5678)),
    ("[$-409]General", N(1234.5678, 45306.5)),
    ("[DBNum1]General", N(1234.5678, 12)),
    ("[DBNum1][$-804]General", N(1234.5678, 12)),
    ("[DBNum2][$-804]General", N(1234.5678)),
    ("[DBNum1][$-411]General", N(1234.5678)),
])

# Rounding of halves, and negative zero.
round_values = N(
    0.5, 1.5, 2.5, -0.5, -1.5, -2.5, 0.125, 0.135, 2.675, 1.005, 0.045, 1.45, 0.285, 8.345, -0.001, -0.004, -0.005,
    -0.0049, 0.0049, 0.005, "-0", 1e-20, -1e-20, 999.995, 0.1 + 0.7, 4.35 * 100, 1234567890123.455, 0.000000000000005,
    1.0000000000000002, 123456789012345.67, 1234567890123456789,
)
sheet("rounding", 100, [
    ("0", round_values),
    ("0.0", round_values),
    ("0.00", round_values),
    ("#,##0.00", round_values),
    ("0%", N(0.005, -0.005, 0.0049, -0.0049, 0.125, "-0")),
    ("0.0E+00", N(0.125, 1.25, -1.25, 9.95, 9.96, 99.95, "-0", -1e-20)),
    ("0.00;(0.00)", N(-0.001, -0.005, -0.0049, "-0", 0.001)),
    ("0;-0", N(-0.4, -0.5, "-0")),
    ("0;(0)", N(-0.4, -0.5)),
    ('0;-0;"zero"', N(-0.4, 0.4, "-0", 0, 1e-20, -1e-20)),
    ('0.0;"neg";"zero"', N(-0.04, 0.04, "-0", 0)),
    ('[<0]"neg";"pos"', N("-0", -1e-20, 0)),
    ("0.00_);(0.00)", N(-0.001, -0.005)),
    ("#", N(0.4, -0.4, "-0", 0)),
    ("#.##", N(0.001, -0.001, 0)),
    ("0.0;-0.0;0.0", N(-0.04, -0.05)),
])

# Placeholders, grouping, scaling, percent, exponents and literals.
nums = N(1234.5678, -1234.5678, 0, 0.05, 1e20, 123456789012345678, 0.000123)
sheet("numbers", 100, [(c, nums) for c in [
    "0", "#", "?", "0.0#", "#.##", "?.??", "00000", "#,##0", "#,###", "#,", "#,##0,", "#,##0,,", '0.0,,"M"', "#,##0.00,",
    "0,0", "#,##0.00;(#,##0.00)", "0%", "0.00%", "%0", "0%%", "0.00E+00", "0.00E-00", "0.0E+0", "##0.0E+0", "#E+0",
    "0E+00", "0.00e+00", "00.00E+000", "#,##0.0E+0", "$#,##0.00", '"$"#,##0.00', BS + "$0", '0.00 "kr"', '0" units"',
    '"x"0"y"', "0" + BS + "x", "0!x", "(0)", "-0", "+0", "0-0", "000-00-0000", "[<=9999999]###-####;(###) ###-####",
    "# ##0", "0 000", "#,##0.00 " + EURO, EURO + "#,##0", "0" + chr(0xB0), "0" + BS + "%", '0"%"', "0.0" + BS + ",",
    BS + "0", "0" + BS + "." + "0", "0.000000000000000000000000000000", "#,##0.00##", "0.0??", "???,???.??",
    "0/0", "0:00", "00" + BS + ":00", "0.00 General", "?,???", "0.##;-0.##;0",
]])

# Fractions' denominators.
frac_values = N(
    0, 0.5, 0.333, 1 / 3, 2 / 3, 0.25, 0.75, 0.2, 0.1, 0.01, 0.001, 0.0001, 1.75, 3.14159265358979, -1.5, -0.25, 0.999,
    0.9999, 12345.678, 1234567.1, 5, 1 / 7, 0.3, 0.37, 0.123456, 2.5, 0.05, 0.99, 0.95, 100.04,
)
sheet("fractions", 100, [(c, frac_values) for c in [
    "# ?/?", "# ??/??", "# ???/???", "?/?", "??/??", "???/???", "# ?/2", "# ?/4", "# ?/8", "# ??/16", "# ?/10",
    "# ??/100", "# #/#", "# 0/0", "0 ?/?", "# ?/3", "?/??", "# ??/?", "0/4", "?/16", '?/?" in"', "# ?/?;(# ?/?)",
    "# ?/?;-# ?/?", "#,### ?/?", "0 ??/??", "#\" \"?/?",
]])

# Elapsed time past its limits, and clock time.
elapsed_values = N(
    0, 0.25, 0.5, 0.99999, 0.999999999, 1, 1.5, 2.25, 10.75, 31.5, 32.5, 366, 1000.5, 1 / 86400, 0.5 / 86400,
    1 + 0.5 / 86400, 1 / 24 - 1e-9, 0.04166666, -0.5, -1.5, 2958465.5, 2958466, 3000000, 1e7, 1e10, 0.0006944444444,
)
sheet("elapsed", 100, [(c, elapsed_values) for c in [
    "[h]:mm:ss", "[h]:mm", "[hh]:mm:ss", "[h]", "[m]:ss", "[mm]:ss", "[m]", "[s]", "[ss]", "[ss].00", "[h]:mm:ss.000",
    "[mm]:ss.0", "h:mm:ss", "mm:ss", "mm:ss.0", "[h]:mm:ss;@", "[h]:mm;-[h]:mm", 'd "days" h:mm', '[h]" hours"',
    "[hhh]:mm", "[h]:mm:ss;[Red]-[h]:mm:ss", "[s].000", "[m]:ss.00", "h:mm:ss.00",
]])

# Dates: before 1900, the 1900 leap-year bug, the last date, rounding.
date_values = N(
    0, 0.5, 1, 2, 31, 59, 59.5, 60, 60.5, 61, 366, 367, 1462, 45306.5, 45306.999994, 45306.99999999, 45306.123456789,
    2958465, 2958465.99999, 2958465.999999999, 2958466, -1, -0.5, 1e10, 45351, 45352, 36526,
)
date_codes = [
    "yyyy-mm-dd", "yyyy-mm-dd hh:mm:ss", "m/d/yyyy", "d-mmm-yy", "dddd, mmmm d, yyyy", "ddd", "mmmmm", "yy", "yyy",
    "y", "e", "d", "dd", "dddd", "mmm", "h:mm:ss.000", "hh:mm:ss.00", "ss.0", "yyyy-mm-dd;@", "m/d/yyyy h:mm",
    "yyyy-mm-dd" + BS + "Thh:mm:ss.000", "b", "bb", "bbbb", "g", "ggg", "aaa", "aaaa", "dd/mm/yyyy", "dd.mm.yyyy",
    "yyyy/mm/dd", "hh:mm", "d mmm yyyy hh:mm AM/PM",
]
sheet("dates", 100, [(c, date_values) for c in date_codes])

# AM/PM and A/P.
ampm_values = N(0, 0.25, 0.5, 13 / 24, 1 - 1 / 86400, 1 / 24, 45306.75, 0.999999, 12.5 / 24)
sheet("ampm", 100, [(c, ampm_values) for c in [
    "h:mm AM/PM", "h:mm:ss AM/PM", "hh:mm AM/PM", "h AM/PM", "h:mm am/pm", "h:mm Am/Pm", "h:mm A/P", "h:mm a/p",
    "AM/PM h:mm", "h:mm:ss A/P", "[$-409]h:mm AM/PM", "[$-407]h:mm AM/PM", "[$-411]h:mm AM/PM", "[$-411]AM/PM h:mm",
    "[$-804]AM/PM h:mm", "[$-40C]h:mm AM/PM", "m/d/yyyy h:mm AM/PM", "h:mm:ss.0 AM/PM", "hh:mm:ss am/pm", "mm:ss AM/PM",
    "h:mm a", "h:mm AM", "h:mm A/P/X", "[$-412]AM/PM h:mm", "[$-F400]h:mm:ss AM/PM", "[$-x-systime]h:mm:ss AM/PM",
    "h:mm:ss AM/PM;@", "AM/PM", "A/P",
]])

# Locale codes and currency symbols.
locale_dates = N(45306.5, 45550.25, 45640)
locale_numbers = N(1234567.891, -1234.5, 0)
sheet("locales", 100, [(c, locale_dates) for c in [
    "[$-409]mmmm d, yyyy", "[$-407]mmmm dddd", "[$-407]mmm ddd", "[$-40C]mmmm dddd", "[$-40C]mmm ddd", "[$-410]mmmm dddd",
    "[$-411]mmmm dddd", "[$-411]ggge" + '"' + NEN + '"' + "m" + '"' + GATSU + '"' + "d" + '"' + NICHI + '"', "[$-411]aaaa",
    "[$-411]aaa", "[$-412]dddd mmmm", "[$-804]dddd mmmm", "[$-419]mmmm", "[$-419]d mmmm yyyy", "[$-415]d mmmm yyyy",
    "[$-405]d. mmmm yyyy", "[$-F800]dddd, mmmm dd, yyyy", "[$-x-sysdate]dddd, mmmm dd, yyyy", "[$-en-US]mmmm",
    "[$-de-DE]mmmm", "[$-1010409]d/m/yyyy", "[$-2010401]d/m/yyyy", "[$-1060409]d/m/yyyy", "[$-409]mmmmm", "[$-407]mmmmm",
    "[$-407]dd/mm/yyyy", "[$-407]yyyy-mm-dd", "[$-40C]yyyy-mm-dd", "[$-41E]d mmmm yyyy", "[$-D07041E]d mmmm yyyy",
    "[$-101041E]d mmmm yyyy", "[$-409]d/m/yyyy",
]] + [(c, locale_numbers) for c in [
    "[$-407]#,##0.00", "[$" + EURO + "-407] #,##0.00", "#,##0.00 [$" + EURO + "-407]", "[$" + EURO + "-2] #,##0.00",
    "[$$-409]#,##0.00", "[$" + POUND + "-809]#,##0.00", "[$" + YEN + "-411]#,##0", "[$" + RUPEE + "-4009] #,##,##0.00",
    "[$CHF-807] #,##0.00", "[$USD] #,##0", "[$-409]0.00", "[$-40C]#,##0.00", "[$kr-41D] #,##0", "[$-411]#,##0",
    "[$-2000000]0", "[$-D000409]0", "[DBNum1][$-804]0", "[DBNum2][$-804]0", "[DBNum3][$-804]0", "[DBNum1][$-411]0",
    "[$-120000]0", "[$-130000]0",
]])

# Built-in formats by id, which the styles part doesn't define.
builtin_values = N(1234.5678, -1234.5678, 0, 45306.5, 0.75) + S("abc")
sheet("builtins", 100, [(("id", i), builtin_values) for i in list(range(0, 82)) + [100, 163]])

# _ and *.
fill_values = N(1234, -1234, 0, 0.5) + S("abc")
fill_codes = [
    "0_)", "0_-", "_(0_)", "_W0", "_x0", "#,##0_);(#,##0)", '_(* #,##0_);_(* (#,##0);_(* "-"_);_(@_)',
    '_("$"* #,##0.00_);_("$"* (#,##0.00);_("$"* "-"??_);_(@_)', "* #,##0", "*-0", "0*-", "0* ", "**0", '*x"a"0',
    '"$"* #,##0.00', "@*-", "*.@", "@_)", "_(@_)", "_-* #,##0_-;-* #,##0_-;_-* " + '"-"' + "_-;_-@_-", "0*" + EURO,
    "*" + chr(0x2550) + "0", "_" + chr(0x5E74) + "0",
]
sheet("fills", 40, [(c, fill_values) for c in fill_codes])
sheet("fills12", 12, [(c, fill_values) for c in fill_codes])

# Conditions, and three and four sections with @.
section_values = N(150, 50, 0, -50, -150, -0.001, 0.001, 5, -5) + S("abc", "") + B + [("e", "#N/A")]
sheet("sections", 100, [(c, section_values) for c in [
    "[>100]0;0.00", '[>=100]"big";"small"', '[<0]"neg";"pos"', '[=0]"zero";General', '[<>0]0;"z"',
    '[>100]"big";[<-100]"small";"mid"', '[>100]"big";[<-100]"small"', "[<=100][Red]0;[Blue]0", "[Red][>100]0;[Blue]0",
    "[>100]0;-0", "[<-100]0;0", "[>5]0", '[>5]"a";[<-5]"b"', '0;-0;"zero";"text: "@', "0;-0;;@", "0;;", ";0", ";;0",
    ";;;", ";;;@", "0;0;0;@", '0;-0;0;"["@"]"', '"pos";"neg";"zero";"text"', "0;@", "0.00;;" + '"zero"',
    "General;General;General;@", '"t:"@"/"@', "@@", '@" "@', "[<10]0.0;[>100]0;General", "[>=10]0;[<=-10]0;0.00",
    "[<0]0;0", '[<1]"lt1";"ge1"', "0;-0;0", "0;-0", '0;"neg"', "[=5]0;[=-5]0;0.000", "[>-10]0;0.0",
]])

# Text, booleans and errors.
sheet("cells", 100, [(c, S("abc", "123", "  lead", "", "=1+1", "0123") + B + E) for c in [
    "General", "0.00", "@", '"a"@"b"', '0;0;0;"t:"@', ";;;", "yyyy-mm-dd", "[h]:mm", "# ?/?", "0%", "[Red]@", "@*-",
    "_(@_)", "0.00E+00", ";;;@", "0;0;0;@@",
]])

# Colors.
sheet("colors", 100, [(c, N(5, -5, 0) + S("abc")) for c in [
    "[Red]0", "[red]0", "[RED]0", "[Blue]0;[Red]-0", "[Color10]0", "[Color 10]0", "[Color56]0", "[Color1]0", "[Green]0",
    "[Cyan]0", "[Magenta]0", "[Yellow]0", "[White]0", "[Black]0", "[Red]General", "[Red]", "[Blue]@", "0;[Red]0",
    "[Color57]0", "[Color0]0",
]])

# A column of Excel's default width (8.43): General shortens, other codes show #.
sheet("narrow", None, [
    ("General", N(1234567.891, 123456789012, 0.1234567891, -0.000012345, 12345678, 1e-20, 99999999.5, 1234567891)),
    ("0.00", N(1234567.891, 12.5)),
    ("yyyy-mm-dd hh:mm:ss", N(45306.5)),
    ("yyyy-mm-dd", N(45306.5, -1)),
    ("@", S("a text longer than the column")),
    ("0", N(-1)),
])

# The 1904 date system: negative dates and times.
dates1904 = N(0, 0.5, 1, -1, -0.5, -1462, -1462.5, 2957003, 2957003.99999, 2957004, 44000, 1462, -1e-9)
SHEETS1904 = {
    "dates1904": (100, [(c, dates1904) for c in [
        "yyyy-mm-dd", "yyyy-mm-dd hh:mm:ss", "m/d/yyyy", "dddd", "h:mm:ss", "[h]:mm:ss", "[h]:mm;-[h]:mm", "[m]:ss",
        "h:mm AM/PM", "General", "0.00", "d", "yyyy", ("id", 14), ("id", 22), ("id", 46), ("id", 20),
    ]]),
}




def probes2():
    values = N(-20, -5, -1, -0.5, 0, 0.5, 5, 20)
    codes = []
    for op in ["<", "<=", ">", ">=", "=", "<>"]:
        for c in ["-10", "-1", "0", "1", "10"]:
            codes.append(f"[{op}{c}]0;0")
    for op in ["<", "<=", ">", ">=", "=", "<>"]:
        for c in ["-10", "0", "10"]:
            codes.append(f"[{op}{c}]0")
    codes += [
        "[>10]0;[<-10]0;0", "[>10]0;[<10]0;0", "[<-10]0;[>10]0;0", "[>=0]0;[<0]0;0", "[<0]0;[>0]0;0", "[>-10]0;[<=-10]0;0",
        "[<10]0;[>=10]0;0", "[=0]0;[<0]0;0", "[<0]0;[=0]0;0", "[>0]0;[=0]0;0", "[<=0]0;0", "[<-10]0;[<-1]0;0",
        '"x"', '"x";"y"', '[>0]"x";"y"', '[<0]"x";"y"', "[<0]0;-0", "[<0]-0;0", "[<-10]0;-0", "[>10]0;-0",
        "[<0]0.0;[>0]0.0;0.0",
    ]
    conditions = [(c, values) for c in codes]

    fraction_values = N(
        0.5, 8191.75, 8192, 16383.75, 16384, 32767.25, 32768.25, 65535.5, 65536.5, 2047.9375, 2048, 4095.9375, 4096,
        1e6 + 0.25, 1e9 + 0.5, 2147483647.5, 123456789.123, 1e15, 1e15 + 0.5, 1e16, -8192.25, 0.000001,
    )
    fractions2 = [(c, fraction_values) for c in ["0/4", "?/16", "# ?/4", "?/?", "# ?/?", "0/0", "??/??", "# ??/??", "# ?/8"]]

    system_values = N(45297.04237, 45306.5, 45640.75, 0.0423, 0.5)
    system = [(c, system_values) for c in [
        "[$-F800]dddd, mmmm dd, yyyy", "[$-F800]yyyy", "[$-F800]dd/mm/yyyy", "[$-F800]General", "[$-F800]0.00",
        "[$-F800]h:mm", "[$-F400]h:mm:ss AM/PM", "[$-F400]hh:mm", "[$-F400]yyyy-mm-dd", "[$-F400]0.00",
        "[$-F800]mmmm", "[$-F400]h", "[$-409]dddd, mmmm dd, yyyy", "dddd, mmmm d, yyyy",
    ]]

    SHEETS2 = {
        "conditions": (100, conditions),
        "fractions2": (100, fractions2),
        "system": (100, system),
    }
    return SHEETS2, {}


def probes3():
    cond_values = N(-150, -50, -5, -0.5, 0, 0.5, 5, 50, 150)
    conditions = [(c, cond_values) for c in [
        "0;[<-5]0", "0;[<-5]0;\"z\"", "0;[>5]0", "0.0;[>100]0", "0;0;[=0]\"z\"", "0;0;[>5]\"z\"",
        "[>100]0;0.00;\"x\"", "[>100]0;0.00;\"x\";@", "[>100]0;0.00;[=0]\"z\"", "[>100]0;;\"x\"", "[>100]0;[<-100]0;",
        "[<0]\"neg\";[>0]\"pos\";\"zero\"", "[=5]\"five\";[=50]\"fifty\";\"other\"", "[>100]General;[<0]0.0",
        "[>0]0;[<0]-0;\"zero\"", "[<=-5]0;[>=5]0", "[<=-5]0;[>=5]0;\"mid\"", "[>=-5]0;0.0",
    ]]

    frac_values = N(0, 0.5, 5, 5.5, -0.5, -5.5, 0.999, -0.001, -0.01, 0.0001, 1e20)
    fraction_codes = [
        '#"x"?/?', '#"x"#/#', '#"x"0/0', '0"x"?/?', '?"x"?/?', '#" - "?/?', '# ?/?" in"', '# #/#" in"', '# 0/0" in"',
        '"a"# ?/?"b"', '#,##0 ?/?', '# #?/?#', '# ?#/#?', '# 0?/?0', '00 ?/?', '? ?/?', '#?/?', '0#/#0', '# ?/? ?',
        '?/?', '0/0', '# ?/?', '# ?/8', '?/8', '# ??/?? "x"',
    ]
    fixed_values = N(0.9, 0.5, 0.1, 0.99999, 1.9, 0.00001, 2.5, -0.9)
    fixed_codes = ["# ?/50000", "?/50000", "# ?/32768", "?/32768", "?/2", "# ?/1", "?/1", "# ?/100000000", "?/30000"]
    limit_values = N(
        2147483647, 2147483648, 1073741823.5, 1073741824.5, 2147483646.5, 536870911.75, 536870912.25, 999999999.75,
        4294967295, 1e10, -2147483647, -2147483648, -2147483649, -1073741824.5, -1073741823.5, 715827882.3333334,
    )
    limit_codes = ["?/?", "??/??", "0/0", "?/??", "# ?/?"]
    whole_values = N(1e20, 1e300, 123456789012345678, 123456789012345.67, 0.9999999999999999, 99999.99999999999)
    fractions3 = (
        [(c, frac_values) for c in fraction_codes]
        + [(c, fixed_values) for c in fixed_codes]
        + [(c, limit_values) for c in limit_codes]
        + [(c, whole_values) for c in ["# ?/?", "?/?", "# ?/8"]]
    )

    number_values = N(1234567.891, 12, 0, -12.5, 0.000123)
    exp_values = N(1234.5, 1e100, 1e-100, 0, 0.000123, 5, -1234.5, 1.7976931348623157e308, 1e-300)
    numbers3 = (
        [(c, number_values) for c in [
            "#,##0-00", "#,##0\" \"000", "0,000-0", "#,#-#", "?,???,???", "0,000,000", "0.0,0", "0.0,", "0,.0", "#,##0,.00",
            "0.0.0", "0..0", "#.#.#", "\"x\".00", ".00", ".##", "0\"a\"0\"b\"0", ",0", ",##0", "0.0%,", "\"a\"General\"b\";\"c\"General",
            "#,##0.0E+0", "0.00\" \"0", "#0.0#0", "0\\.0\\.0", "#,##0.00;-#,##0.00;\"zero\"", "0;-0;;\"t\"",
        ]]
        + [(c, exp_values) for c in [
            "0.0E+??", "0.0E+##", "0.0E+0?", "0.0E+?0", "0.0E+#0", "?.?E+0", "#.#E+0", "0.0E+000", "0.0E+0\" u\"",
            "\"x\"0.0E+0", "0.0E+0%", "0.00E+00", "General", "0", "##0.0E+0", "0.0E-0", "?0.0E+0", "#,##0.0E+00",
        ]]
    )

    SHEETS3 = {
        "conditions3": (100, conditions),
        "fractions3": (100, fractions3),
        "numbers3": (100, numbers3),
    }
    return SHEETS3, {}


def probes4():
    def big(digits):
        """1 followed by zeros, as a number of that many digits."""
        return "1E+" + str(digits - 1)


    limits4 = [
        ("0", N(big(254), big(255), big(256), "-" + big(254), "-" + big(255))),
        ('"ab"0', N(big(252), big(253), big(254))),
        ("0.0", N(big(252), big(253), big(254))),
        ("#,##0", N(big(190), big(191), big(192), big(193))),
        ("# ?/?", N(big(250), big(251), big(252))),
        ("0.00000000000000000000000000000000000000000000000000", N(big(203), big(204), big(205))),
        ("@", S("a" * 254, "b" * 255, "c" * 256, "d" * 300, "e" * 1100)),
        ('"x"@', S("a" * 254, "b" * 255, "c" * 256, "d" * 300, "e" * 1100)),
        ('@"y"@', S("a" * 127, "b" * 128, "c" * 200)),
    ]

    fixed_values = N(0.5, 0.9, 0.1, 0.123, 0.75)
    fixed4 = [(f"?/{d}", fixed_values) for d in [
        10000, 20000, 25000, 32767, 32769, 33000, 35000, 40000, 45000, 49999, 50000, 65535, 65536, 99999,
    ]] + [(f"# ?/{d}", fixed_values) for d in [32769, 40000, 99999]]

    den_values = N(0.5, 0.37, 0.123456, 0, 5, 3.14159265358979)
    denominators4 = [(c, den_values) for c in [
        "?/#?", "?/?#", "?/0?", "?/#0", "?/?0", "?/??0", "?/0#", "?/00", "?/##", "#?/??", "?#/??", "0#/??", "#0/??",
        "# ?/#?", "# ?/?0", "# ??/#0",
    ]] + [(c, N(1234.5, 0.05, 0, 1e100)) for c in ["%0.0E+0", "0.0E+0%%", "0.0E+0\" %\"", "0%"]]

    SHEETS4 = {
        "limits4": (100, limits4),
        "fixed4": (100, fixed4),
        "denominators4": (100, denominators4),
    }
    return SHEETS4, {}


def probes5():
    def big(digits):
        """1 followed by zeros, as a number of that many digits."""
        return "1E+" + str(digits - 1)


    exponents5 = [(c, N(1234.5, 0.05, 0, -5, 1e100)) for c in ["0E+", "0.0E+", "0.00E-", '0E+" x"', "#E+", "0E+;0"]]
    subnormal_values = N("5E-324", "-5E-324", "2.2E-308", "-2.2E-308", "-1E-307", "2.2250738585072014E-308", "-2.2250738585072014E-308")
    subnormals5 = [(c, subnormal_values) for c in ['0;-0;"zero"', '[<0]"neg";"pos"', "General", '0.0E+00;-0.0E+00;"zero"']]

    SHEETS4 = {
        "exponents5": (100, exponents5),
        "subnormals5": (100, subnormals5),
    }
    return SHEETS4, {}


def probes6():
    SECOND = 1 / 86400

    section_codes = [
        "0;yyyy-mm-dd", "yyyy-mm-dd;yyyy-mm-dd", 'yyyy-mm-dd;"neg"', "h:mm;h:mm", "[h]:mm;[h]:mm", "[h];[h]", "[h];-[h]",
        "[h];0", "0;[h]", '[<0]"neg";yyyy-mm-dd', "[>=0]yyyy-mm-dd;0", "[<0]yyyy-mm-dd;0", "[<0]h:mm;0", "[Red]yyyy-mm-dd",
        'yyyy-mm-dd;;"zero"', '"Date: "yyyy-mm-dd', '"x"[h]', '[h]" h";-[h]" h"', "[$-F800]dddd", "[$-F400]h:mm",
        "[$-F800]dddd;0", "0;[$-F800]dddd", "yyyy-mm-dd;-yyyy-mm-dd", "dddd;dddd", "[h]:mm:ss", "[h]", "[s].00",
    ]
    section_values = N(-1, -0.5, -1e-9, "-5E-324", 0, 0.5, 45306.5, 2958465.99999, 2958466)

    elapsed_codes = ["[h]", "[m]", "[s]", "[hh]", '[h]" h"', "[h];[h]", '"x"[s]']
    elapsed_values = N(
        1e12, 1e15, 1.23456789012345e15, 1e16, 1e20, 1.23456789012345e20, 1e100, 1e249, 1e250, 1e251, 1e252, 1e253, 1e300,
        "1.7976931348623157E+308", -1e15, -1e250, -1e-9, -1e-5, -0.5 * SECOND, 0.5 * SECOND, -1.5 * SECOND, 1.5 * SECOND,
        2.5 * SECOND, -2.5 * SECOND, 0.4999 * SECOND, 1 / 48, 0.75, 2958465.5, 2958466, 123456.789,
    )

    ofday_codes = [
        "yyyy-mm-dd [h]:mm", "d [h]", "m/d [m]", "yyyy [s]", "[h] yyyy", "[h]:mm d", "dddd [h]", "mmm [hh]:mm:ss",
        "yyyy [h]:mm:ss.0", "d [h] AM/PM",
    ]
    ofday_values = N(0, 0.75, 1.5, 45306.75, 45306.999994, 2958465.5, 2958466, -0.5, 1e10)

    token_codes = [
        "hh:mm:mm", "h d m", "h mmm", "m ss", "ss m", "mm", "m", 'h "x" m', "h 0 m", "[h] m", "s m", "h:m:s", "yyyyy",
        "yyyyyy", "ddddd", "dddddd", "mmmmmm", "mmmmmmm", "aa", "a", "bbb", "bbbbb", "eee", "ee", "hhh", "sss",
        "a/P", "A/p", "[h]:mm AM/PM", "h:mm:ss.0000", "h:mm.00", "yyyy.00", "yyyy 0.0", "0.0 yyyy", "yyyy 0", "yyyy #",
        "yyyy ?", "yyyy General", "yyyy 0%", "h:mm%", "yyyy,", "yyyy E+0", "e+0", "B1yyyy-mm-dd", "B2yyyy-mm-dd",
        "[$-411]mmmmm", "[$-411]mmm", "[$-411]ddd", "[$-412]mmm", "[$-412]ddd", "[$-404]e", "[$-404]yyyy", "[$-411]e",
        "[$-411]g", "[$-804]mmm", "[$-419]mmm", "[$-419]ddd", "[$-419]mmmmm", "h:mm:ss.000.0", "ss.0.0",
    ]
    token_values = N(0.5, 45306.5208333, 45306.999994, 1234.5678, 45550.25)

    rounding_codes = ["ss.000", "h:mm:ss", "ss.00", "ss.0", "[s].000", "[s]", "h:mm:ss.000"]
    rounding_values = N(
        0.0005 * SECOND, 0.0015 * SECOND, 0.0025 * SECOND, 0.4995 * SECOND, 0.9995 * SECOND, 59.9995 * SECOND,
        1.5 * SECOND, 2.5 * SECOND, 45306 + 0.0005 * SECOND, 45306 + 2.5 * SECOND, 45306 + 0.5 * SECOND, 0.125 * SECOND,
        0.25 * SECOND, 0.005 * SECOND, 0.015 * SECOND, 0.025 * SECOND, 0.035 * SECOND,
    )

    # Wednesday has 9 letters: 28 of them are 252 characters.
    length_codes = ["dddd" * 28 + '"xyz"', "dddd" * 28 + '"xyzw"', "dddd" * 28, '"' + "x" * 248 + '"[h]']
    length_values = N(45308.5, 45306.5, 1e3, 1e4)

    limit_codes = ["yyyy-mm-dd", "h:mm:ss", "[h]:mm", "[h]", "dddd", "[$-F800]dddd", "[$-F400]h:mm", "yyyy-mm-dd;\"neg\""]
    limit_values = N(-2957003.99999, -2957004, -2957003.999999999, 2957003.999999999, -1e10, 1e10, -1462.25)

    SHEETS = {
        "sections6": (100, [(c, section_values) for c in section_codes]),
        "elapsed6": (255, [(c, elapsed_values) for c in elapsed_codes]),
        "ofday6": (100, [(c, ofday_values) for c in ofday_codes]),
        "tokens6": (100, [(c, token_values) for c in token_codes]),
        "rounding6": (100, [(c, rounding_values) for c in rounding_codes]),
        "length6": (255, [(c, length_values) for c in length_codes]),
    }
    SHEETS1904 = {
        "sections1904": (100, [(c, section_values) for c in section_codes]),
        "limits1904": (100, [(c, limit_values) for c in limit_codes]),
    }
    return SHEETS, SHEETS1904


def probes7():
    X200 = '"' + "x" * 200 + '"'
    length_codes = [
        'dddd""' * 27 + 'dddd"xy"', 'dddd""' * 27 + 'dddd"xyz"', 'dddd""' * 27 + 'dddd"xyzw"', X200 + "[h]", X200 + "0",
        X200 + "[s];" + X200 + "[s]",
    ]
    length_values = N(45308.5, 45306.5, 1e51, 1e52, 1e53, 1e54, 1e55, -1e51, -1e52, -1e53, -1e54)

    minute_codes = [
        "h yyyy m", "h mmm m", "h dddd m", "s d m", "h d d m", "h:mm d m", "[h] d m", "[s] m", "m [s]", "m yyyy s", "m d s",
        "m h", "d m", "h AM/PM m", "m:s.0", "yyyy m", "h \"m\" m", "h\\mm", "h@m", "mm ss mm", "h mm mm", "s mm", "[mm] m",
        "h mmmmm m", "e m", "b m", "ggg h m",
    ]
    minute_values = N(45306.5208333, 45550.75, 0.0423)

    system_codes = [
        "0;0;[$-F800]dddd", "[$-F800]dddd;[$-F400]h:mm;[$-F800]yyyy", "[$-F400]h:mm;[$-F800]dddd", "0;[$-F400]h:mm",
        "[$-F800];0", "[$-F800]", "[$-F400]", "[$-F800]\"x\"", "[$-F800]@", "@;[$-F800]",
    ]
    system_values = N(-1, -0.5, 0, 1, 45306.5)

    SHEETS = {
        "length7": (255, [(c, length_values) for c in length_codes]),
        "minutes7": (100, [(c, minute_values) for c in minute_codes]),
        "system7": (100, [(c, system_values) for c in system_codes]),
    }
    SHEETS1904 = {
        "system1904": (100, [(c, system_values) for c in system_codes]),
    }
    return SHEETS, SHEETS1904


def probes8():
    def length7e():
        x99 = '"' + "x" * 99 + '"'
        codes = ["[$-416]\"" + "x" * size + '"' + "dddd " * 14 + "dddd" for size in (58, 59, 60, 61, 62)]
        codes += [x99 + "[h]", x99 + "0", x99 + "[s]", x99 + "yyyy"]
        values = N(45308.5, 1e152, 1e153, 1e154, 1e155, 1e156, -1e152, -1e153, -1e154)

        return 255, [(c, values) for c in codes]


    def length7f():
        groups = []
        for digits in (157, 180, 200, 220, 240, 245, 250, 251, 252, 253):
            size = 255 - digits
            value = N("1E+%d" % (digits - 2))
            groups.append(('"' + "x" * size + '"[h]', value))
            groups.append(('[h]"' + "x" * size + '"', value))
            groups.append(('"' + "x" * (size - 1) + '"[h]', value))

        return 255, groups


    def length7g():
        # [h] of 1E+k has k+2 digits (2.4E+k+1), [m] of 1E+k has k+4 (1.44E+k+3), [s] of 1E+k has k+5 (8.64E+k+4).
        groups = []
        for unit, extra in (("h", 2), ("m", 4), ("s", 5)):
            for digits in (244, 245, 246, 247, 248, 249, 250):
                groups.append(('"' + "x" * (255 - digits) + '"[' + unit + "]", N("1E+%d" % (digits - extra))))

        return 255, groups


    def elapsed8():
        codes = ["[h]:mm:ss", "[mm]:ss", "[s] yyyy", '[ss]"s" d', "[s]", "h:mm:ss"]
        values = N(45306.000005787035, 45306.00002893519, 1.0000057870370371, 2958465.0000057870, 1234.0000057870)

        return 100, [(c, values) for c in codes]
    return {"length7e": length7e(), "length7f": length7f(), "length7g": length7g(), "elapsed8": elapsed8()}, {}


def probes9():
    DAYS = [0, 1, 61, 45306, 45319, 2958465]
    # Seconds whose shown digits end in a half, by the digits of fractions shown.
    TIMES = {
        0: ["0.5", "1.5", "2.5", "7.5", "59.5", "32707.5", "43199.5"],
        1: ["0.05", "0.25", "7.25", "32707.25", "59.95"],
        2: ["0.005", "0.125", "7.125", "32707.245"],
        3: ["0.0005", "7.2505", "32707.2505"],
    }
    OFFSETS = ["-1E-2", "-1E-4", "-1E-6", "-1E-8", "-1E-10", "-1E-12", "0", "1E-12", "1E-10", "1E-8", "1E-6", "1E-4", "1E-2"]
    CODES = {0: ["h:mm:ss"], 1: ["h:mm:ss.0", "mm:ss.0"], 2: ["h:mm:ss.00"], 3: ["h:mm:ss.000"]}


    def value(day, seconds, k, offset):
        unit = Fraction(1, 10 ** k)
        exact = Fraction(day) + (Fraction(seconds) + Fraction(offset) * unit) / 86400

        return num(float(exact))


    def halves9():
        groups = []
        for k, codes in CODES.items():
            values = [value(day, t, k, o) for day in DAYS for t in TIMES[k] for o in OFFSETS]
            for code in codes:
                groups.append((code, N(*values)))

        return 100, groups
    return {"halves9": halves9()}, {}


def probes10():
    """Step 7's truncation probe: serials near halves where truncating the time's hours or minutes, in doubles, gives
    another time than rounding them (found by a seeded search, make_probes10.py), each in the code of its precision."""
    values = {
        "h:mm:ss": [
            "0.15883680555555554", "0.27982060185185187", "0.0357349537037037", "0.03787615740740741",
            "0.2904803240740741", "0.0357349537037037", "0.035445601851851846", "0.0669849537037037",
            "0.19008680555555554", "0.0824363425925926", "0.035758101851851846", "0.02777199074074074",
            "0.1594386574074074", "0.0004571759259259258", "0.0005034722222222222", "0.00048032407407407404",
        ],
        "h:mm:ss.0": [
            "0.041427662037037044", "0.03848553240740741", "0.11371585648148148", "0.20141840277777778",
            "0.1220167824074074", "0.32913252314814817", "0.11357465277777777", "0.19747280092592592",
            "0.02900173611111111", "0.19928877314814814", "0.011094328703703703", "0.002060763888888889",
            "0.0006753472222222221", "1.2826221064814816", "0.10553877314814815", "0.3972054398148148",
        ],
        "h:mm:ss.00": [
            "0.03436082175925926", "0.3296927662037037", "0.07776059027777778", "0.023234780092592593",
            "0.023789409722222224", "0.0265359375", "0.0641990162037037", "0.03355596064814815",
            "0.06830445601851852", "0.04051568287037037", "0.0811708912037037", "0.23252621527777778",
            "0.039517881944444444", "0.1048529513888889", "0.12148327546296296", "0.006860590277777778",
            "0.9492751736111111", "0.4902766782407407", "0.17971568287037037", "0.05552621527777778",
        ],
        "h:mm:ss.000": [
            "0.19934027199074073", "0.07655737847222223", "0.041560804398148146", "0.2380642650462963",
            "0.03079993634259259", "0.02778476273148148", "0.0256183275462963", "0.03478524884259259",
            "0.24422976273148148", "0.028373871527777778", "0.12175843171296297", "0.03143652199074074",
            "0.03778493634259259", "1.731793420138889", "0.07914585069444445", "0.002470792824074074",
            "0.002472818287037037", "0.0005862210648148147", "0.025443385416666665", "0.019950109953703703",
        ],
    }
    return {"truncation10": (100, [(code, N(*vs)) for code, vs in values.items()])}, {}

def probes11():
    """Step 7's probe of the seconds' truncation: the nine serials near halves of ten million (make_probes11.py's seeded
    search) where truncating or rounding the whole seconds, in doubles, gives another time."""
    values = {
        "h:mm:ss.00": [
            "2.2858796296296297E-05", "1.070601851851852E-05", "0.00017262731481481483",
        ],
        "h:mm:ss.000": [
            "1.808449074074074E-05", "0.000400005787037037", "0.00040000578703703704", "0.0001135474537037037",
            "1.1394675925925924E-05", "0.007357471064814816",
        ],
    }
    return {"seconds11": (100, [(code, N(*vs)) for code, vs in values.items()])}, {}

def esc(text):
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def attr(text):
    return esc(text).replace('"', "&quot;")


def col(n):
    s = ""
    while n:
        n, r = divmod(n - 1, 26)
        s = chr(65 + r) + s
    return s


def inline(ref, value):
    space = ' xml:space="preserve"' if value != value.strip() else ""
    return f'<c r="{ref}" t="inlineStr"><is><t{space}>{esc(value)}</t></is></c>'


def write(path, sheets, date1904, refused, cases):
    # Styles: xf 0 General; then one xf per distinct format.
    codes = {}
    xfs = ['<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>']
    style_of = {}

    def style(fmt):
        key = fmt if isinstance(fmt, str) else ("id", fmt[1])
        if key in style_of:
            return style_of[key]
        if isinstance(fmt, str):
            if fmt not in codes:
                codes[fmt] = 164 + len(codes)
            fid = codes[fmt]
        else:
            fid = fmt[1]
        xfs.append(f'<xf numFmtId="{fid}" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>')
        style_of[key] = len(xfs) - 1
        return style_of[key]

    parts = {}
    names = list(sheets)
    for si, name in enumerate(names, 1):
        width, groups = sheets[name]
        rows = []
        r = 0
        for fmt, values in groups:
            if isinstance(fmt, str) and fmt in refused:
                continue
            s = style(fmt)
            label = fmt if isinstance(fmt, str) else f"id {fmt[1]}"
            for kind, value in values:
                r += 1
                case_id = f"{name}:{r}"
                cells = [inline(f"A{r}", case_id), inline(f"B{r}", label)]
                ref = f"C{r}"
                if kind == "n":
                    cells.append(f'<c r="{ref}" s="{s}"><v>{value}</v></c>')
                elif kind == "b":
                    cells.append(f'<c r="{ref}" s="{s}" t="b"><v>{value}</v></c>')
                elif kind == "e":
                    cells.append(f'<c r="{ref}" s="{s}" t="e"><v>{esc(value)}</v></c>')
                elif value == "":
                    cells.append(f'<c r="{ref}" s="{s}" t="inlineStr"><is><t></t></is></c>')
                else:
                    space = ' xml:space="preserve"' if value != value.strip() else ""
                    cells.append(f'<c r="{ref}" s="{s}" t="inlineStr"><is><t{space}>{esc(value)}</t></is></c>')
                rows.append(f'<row r="{r}">{"".join(cells)}</row>')
                cases.append({
                    "file": os.path.basename(path), "sheet": name, "row": r, "id": case_id,
                    "code": fmt if isinstance(fmt, str) else None, "builtin": None if isinstance(fmt, str) else fmt[1],
                    "kind": kind, "value": value, "width": width,
                })
        cols = '<cols><col min="1" max="2" width="30" customWidth="1"/>'
        if width is not None:
            cols += f'<col min="3" max="3" width="{width}" customWidth="1"/>'
        cols += "</cols>"
        parts[f"xl/worksheets/sheet{si}.xml"] = (
            DECL + f'<worksheet xmlns="{MAIN}" xmlns:r="{REL}">{cols}<sheetData>{"".join(rows)}</sheetData>'
            '<pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3"/></worksheet>')

    numfmts = "".join(f'<numFmt numFmtId="{i}" formatCode="{attr(c)}"/>' for c, i in codes.items())
    styles = (
        DECL + f'<styleSheet xmlns="{MAIN}"><numFmts count="{len(codes)}">{numfmts}</numFmts>'
        '<fonts count="1"><font><sz val="11"/><name val="Calibri"/><family val="2"/></font></fonts>'
        '<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>'
        '<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>'
        '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>'
        f'<cellXfs count="{len(xfs)}">{"".join(xfs)}</cellXfs>'
        '<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>')
    pr = '<workbookPr date1904="1"/>' if date1904 else "<workbookPr/>"
    sheets_xml = "".join(f'<sheet name="{n}" sheetId="{i}" r:id="rId{i}"/>' for i, n in enumerate(names, 1))
    workbook = DECL + f'<workbook xmlns="{MAIN}" xmlns:r="{REL}">{pr}<sheets>{sheets_xml}</sheets></workbook>'
    rels = "".join(
        f'<Relationship Id="rId{i}" Type="{REL}/worksheet" Target="worksheets/sheet{i}.xml"/>' for i in range(1, len(names) + 1))
    rels += f'<Relationship Id="rId{len(names) + 1}" Type="{REL}/styles" Target="styles.xml"/>'
    overrides = "".join(
        f'<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="{CT}worksheet+xml"/>' for i in range(1, len(names) + 1))
    types = (
        DECL + '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
        '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
        '<Default Extension="xml" ContentType="application/xml"/>'
        f'<Override PartName="/xl/workbook.xml" ContentType="{CT}sheet.main+xml"/>'
        f'<Override PartName="/xl/styles.xml" ContentType="{CT}styles+xml"/>{overrides}</Types>')
    entries = {
        "[Content_Types].xml": types,
        "_rels/.rels": DECL + f'<Relationships xmlns="{PKG}"><Relationship Id="rId1" '
                       f'Type="{REL}/officeDocument" Target="xl/workbook.xml"/></Relationships>',
        "xl/workbook.xml": workbook,
        "xl/_rels/workbook.xml.rels": DECL + f'<Relationships xmlns="{PKG}">{rels}</Relationships>',
        "xl/styles.xml": styles,
        **parts,
    }
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        for name, xml in entries.items():
            z.writestr(zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0)), xml, zipfile.ZIP_DEFLATED)



def all_sheets():
    """Every sheet, in the order the probes wrote them: its name, column C's width, its cases, and its date system."""
    out = [(name, width, groups, False) for name, (width, groups) in SHEETS.items()]
    out += [(name, width, groups, True) for name, (width, groups) in SHEETS1904.items()]
    for probes in (probes2, probes3, probes4, probes5, probes6, probes7, probes8, probes9, probes10, probes11):
        sheets, sheets1904 = probes()
        out += [(name, width, groups, False) for name, (width, groups) in sheets.items()]
        out += [(name, width, groups, True) for name, (width, groups) in sheets1904.items()]
    return out


def main():
    folder, cases_path = sys.argv[1], sys.argv[2]
    os.makedirs(folder, exist_ok=True)
    cases = []
    for name, width, groups, date1904 in all_sheets():
        # A sheet of codes Excel refuses alone (exponents5, length7) has no case left.
        if all(isinstance(fmt, str) and fmt in REFUSED for fmt, _ in groups):
            continue
        written = []
        write(os.path.join(folder, name + ".xlsx"), {name: (width, groups)}, date1904, REFUSED, written)
        for case in written:
            case["date1904"] = date1904
        cases += written
    with open(cases_path, "w", encoding="utf-8") as f:
        json.dump(cases, f, ensure_ascii=False, indent=0)
    print(len(cases), "cases in", len({c["file"] for c in cases}), "workbooks")


if __name__ == "__main__":
    main()

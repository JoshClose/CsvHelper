"""Writes the corpus's Python files from the basic, typed and messy workbooks' JSON:
python -I corpus.py <basic.json> <typed.json> <messy.json> <Files folder>.

openpyxl/basic.xlsx, xlsxwriter/basic.xlsx, xlsxwriter/constant_memory.xlsx, and pandas/openpyxl.xlsx and
pandas/xlsxwriter.xlsx (each engine named, since pandas picks XlsxWriter when it is installed); openpyxl/typed.xlsx,
openpyxl/typed-isodates.xlsx (with iso_dates, which writes dates as t="d") and xlsxwriter/typed.xlsx. Each writes what
its library can: openpyxl can't write a control character (it raises IllegalCharacterError), a built-in number format
id outside its table, a dynamic array or a table's formulas, and writes formulas without cached results; XlsxWriter
writes the results Excel computes, and no error constants; pandas writes a data frame per sheet.
"""

import datetime
import json
import os
import re
import sys

import openpyxl
import pandas
import xlsxwriter
from openpyxl.cell.rich_text import CellRichText, TextBlock
from openpyxl.cell.text import InlineFont
from openpyxl.chart import BarChart, Reference
from openpyxl.workbook.defined_name import DefinedName
from openpyxl.worksheet.formula import ArrayFormula

# The cached results of the basic workbook's formulas, as Excel computes them.
RESULTS = {"NA()": "#N/A", "1/0": "#DIV/0!", "B3*2": 84, 'A2&"!"': "text!", "B2+1": "#VALUE!"}


def result(formula, row):
	return row * 10 if formula == "ROW()*10" else RESULTS[formula]


def cells(sheet, features):
	for row in sheet["rows"]:
		yield row["row"], [c for c in row["cells"] if c.get("feature") is None or c["feature"] in features]


def date(cell):
	# A time alone is its serial, a number.
	return datetime.datetime.fromisoformat(cell["date"]) if cell.get("date") else cell["number"]


def save(path):
	print("  " + path)


def write_openpyxl(spec, folder):
	path = os.path.join(folder, "basic.xlsx")
	workbook = openpyxl.Workbook()
	workbook.remove(workbook.active)
	workbook.properties.creator = "CsvHelper.Excel.Corpus"
	worksheets = {}
	for sheet in spec:
		if sheet["kind"] != "worksheet":
			continue
		ws = workbook.create_sheet(sheet["name"])
		ws.sheet_state = sheet["state"]
		worksheets[sheet["name"]] = ws
		for row, row_cells in cells(sheet, {"long", "rich", "formula", "date"}):
			for cell in row_cells:
				target = ws[cell["column"] + str(row)]
				kind = cell["type"]
				if kind == "Text":
					target.value = cell["text"]
				elif kind in ("Number", "Bool"):
					target.value = cell["number"] if kind == "Number" else cell["bool"]
				elif kind == "Formula":
					target.value = "=" + cell["formula"]
				elif kind == "Date":
					target.value = date(cell)
					target.number_format = cell["format"]
				elif kind == "Rich":
					target.value = CellRichText([TextBlock(InlineFont(b=run.get("bold", False)), run["text"]) for run in cell["runs"]])
	index = next(i for i, s in enumerate(spec) if s["kind"] == "chartsheet")
	chartsheet = workbook.create_chartsheet(spec[index]["name"], index)
	chart = BarChart()
	chart.add_data(Reference(worksheets["Q1 Orders"], min_col=2, min_row=1, max_row=4), titles_from_data=True)
	chartsheet.add_chart(chart)
	workbook.save(path)
	save(path)


def write_xlsxwriter(spec, folder, name, options):
	path = os.path.join(folder, name)
	workbook = xlsxwriter.Workbook(path, options)
	workbook.set_properties({"author": "CsvHelper.Excel.Corpus"})
	bold = workbook.add_format({"bold": True})
	formats = {}
	for sheet in spec:
		if sheet["kind"] == "chartsheet":
			chartsheet = workbook.add_chartsheet(sheet["name"])
			chart = workbook.add_chart({"type": "column"})
			chart.add_series({"values": "='Q1 Orders'!$B$2:$B$4", "categories": "='Q1 Orders'!$A$2:$A$4"})
			chartsheet.set_chart(chart)
			continue
		ws = workbook.add_worksheet(sheet["name"])
		if sheet["state"] == "hidden":
			ws.hide()
		elif sheet["state"] == "veryHidden":
			ws.very_hidden()
		for row, row_cells in cells(sheet, {"control", "long", "rich", "formula", "date"}):
			for cell in row_cells:
				at = cell["column"] + str(row)
				kind = cell["type"]
				if kind == "Text":
					ws.write_string(at, cell["text"])
				elif kind == "Number":
					ws.write_number(at, cell["number"])
				elif kind == "Bool":
					ws.write_boolean(at, cell["bool"])
				elif kind == "Formula":
					ws.write_formula(at, "=" + cell["formula"], None, result(cell["formula"], row))
				elif kind == "Date":
					if cell["format"] not in formats:
						formats[cell["format"]] = workbook.add_format({"num_format": cell["format"]})
					if cell.get("date"):
						ws.write_datetime(at, date(cell), formats[cell["format"]])
					else:
						ws.write_number(at, cell["number"], formats[cell["format"]])
				elif kind == "Rich":
					parts = []
					for run in cell["runs"]:
						if run.get("bold"):
							parts.append(bold)
						parts.append(run["text"])
					ws.write_rich_string(at, *parts)
	workbook.close()
	save(path)


def write_pandas(spec, folder):
	frames = {}
	for sheet in spec:
		if sheet["kind"] != "worksheet" or not sheet["rows"]:
			continue
		rows = [{c["column"]: value(c) for c in row_cells} for _, row_cells in cells(sheet, {"long", "date"})]
		header, body = rows[0], rows[1:]
		columns = sorted({k for r in rows for k in r}, key=lambda k: (len(k), k))
		frame = pandas.DataFrame([[r.get(c) for c in columns] for r in body], columns=[header.get(c, c) for c in columns])
		frames[sheet["name"]] = frame
	for engine in ("openpyxl", "xlsxwriter"):
		path = os.path.join(folder, engine + ".xlsx")
		with pandas.ExcelWriter(path, engine=engine) as writer:
			for name, frame in frames.items():
				frame.to_excel(writer, sheet_name=name, index=False)
		save(path)


def value(cell):
	kind = cell["type"]
	if kind == "Text":
		return cell["text"]
	if kind == "Number":
		return cell["number"]
	if kind == "Bool":
		return cell["bool"]
	if kind == "Date":
		return date(cell)
	return None


def write_typed_openpyxl(typed, folder, name, iso_dates):
	path = os.path.join(folder, name)
	workbook = openpyxl.Workbook(iso_dates=iso_dates)
	workbook.remove(workbook.active)
	workbook.properties.creator = "CsvHelper.Excel.Corpus"
	for defined, value in typed["names"].items():
		workbook.defined_names[defined] = DefinedName(defined, attr_text=value)
	for sheet in typed["sheets"]:
		ws = workbook.create_sheet(sheet["name"])
		for row, row_cells in cells(sheet, {"error", "formula", "array", "name", "newer", "date"}):
			for cell in row_cells:
				at = cell["column"] + str(row)
				target = ws[at]
				kind = cell["type"]
				if kind == "Text":
					# openpyxl would take a text such as "#N/A" for an error.
					target.value = cell["text"]
					target.data_type = "s"
				elif kind in ("Number", "Bool"):
					target.value = cell["number"] if kind == "Number" else cell["bool"]
				elif kind == "Error":
					target.value = cell["text"]
				elif kind == "Date":
					target.value = date(cell)
					target.number_format = cell["format"]
				elif kind == "Formula":
					if cell.get("array"):
						target.value = ArrayFormula(cell["array"], "=" + cell["formula"])
					else:
						target.value = "=" + cell["formula"]
	workbook.save(path)
	save(path)


def write_typed_xlsxwriter(typed, folder):
	path = os.path.join(folder, "typed.xlsx")
	workbook = xlsxwriter.Workbook(path, {"use_future_functions": True})
	workbook.set_properties({"author": "CsvHelper.Excel.Corpus"})
	for defined, value in typed["names"].items():
		workbook.define_name(defined, "=" + value)
	formats = {}
	for sheet in typed["sheets"]:
		ws = workbook.add_worksheet(sheet["name"])
		tables = sheet.get("tables", [])
		for row, row_cells in cells(sheet, {"formatId", "formula", "array", "spill", "name", "table", "newer", "date"}):
			for cell in row_cells:
				at = cell["column"] + str(row)
				kind = cell["type"]
				if kind == "Text":
					ws.write_string(at, cell["text"])
				elif kind == "Number":
					ws.write_number(at, cell["number"])
				elif kind == "Bool":
					ws.write_boolean(at, cell["bool"])
				elif kind == "Date":
					key = cell.get("formatId", cell.get("format"))
					if key not in formats:
						formats[key] = workbook.add_format({"num_format": key})
					if cell.get("date"):
						ws.write_datetime(at, date(cell), formats[key])
					else:
						ws.write_number(at, cell["number"], formats[key])
				elif kind == "Formula":
					formula = "=" + cell["formula"]
					if cell.get("array"):
						ws.write_array_formula(cell["array"], "{" + formula + "}", None, cell["result"])
					elif cell.get("spill"):
						# XlsxWriter has its users write the spill operator as ANCHORARRAY(), as a file stores it.
						formula = re.sub(r"(\$?[A-Z]+\$?[0-9]+)#", r"ANCHORARRAY(\1)", formula)
						ws.write_dynamic_array_formula(at + ":" + at, formula, None, cell["result"])
					elif cell.get("feature") == "table" and any(in_range(at, t["range"]) for t in tables):
						# add_table writes a table's calculated column.
						continue
					else:
						ws.write_formula(at, formula, None, cell["result"])
		for table in tables:
			first, last = table["range"].split(":")
			header_row = int("".join(c for c in first if c.isdigit()))
			headers = {}
			formulas = {}
			for row, row_cells in cells(sheet, {"table"}):
				for cell in row_cells:
					at = cell["column"] + str(row)
					if not in_range(at, table["range"]):
						continue
					if row == header_row:
						headers[cell["column"]] = cell["text"]
					elif cell["type"] == "Formula":
						formulas[cell["column"]] = "=" + cell["formula"]
			columns = [{"header": headers[c], **({"formula": formulas[c]} if c in formulas else {})} for c in sorted(headers, key=lambda k: (len(k), k))]
			ws.add_table(table["range"], {"name": table["name"], "columns": columns})
	workbook.close()
	save(path)


def merged_values(sheet):
	# Each merged range's top-left cell, by reference.
	return {merge.split(":")[0] for merge in sheet.get("merges", [])}


def table_names(sheet, table):
	# A table's column names: its header cells' texts, or the names a hidden header row has in no cell.
	if table.get("hideHeader"):
		return table["columns"]
	first, last = table["range"].split(":")
	first_column, header_row = split(first)
	last_column, _ = split(last)
	texts = {}
	for row, row_cells in cells(sheet, {"formula", "table"}):
		if row == header_row:
			for cell in row_cells:
				texts[cell["column"]] = cell["text"]
	return [texts[c] for c in columns_between(first_column, last_column)]


def columns_between(first, last):
	return [letters(i) for i in range(index(first), index(last) + 1)]


def index(column):
	value = 0
	for c in column:
		value = value * 26 + ord(c) - ord("A") + 1
	return value


def letters(column):
	name = ""
	while column > 0:
		column, rest = divmod(column - 1, 26)
		name = chr(ord("A") + rest) + name
	return name


def write_messy_openpyxl(messy, folder):
	"""openpyxl/messy.xlsx: merged ranges, hidden rows and columns, a collapsed group, the tables (with a totals row and a
	hidden header row), a filter whose rows it hides itself, zeroHeight and the columns from D hidden, every defined name
	(the print area and titles through its own API), and the chartsheet. Formulas have no cached results."""
	from openpyxl.worksheet.filters import AutoFilter, CustomFilter, CustomFilters, FilterColumn
	from openpyxl.worksheet.table import Table, TableColumn, TableStyleInfo

	path = os.path.join(folder, "messy.xlsx")
	workbook = openpyxl.Workbook()
	workbook.remove(workbook.active)
	workbook.properties.creator = "CsvHelper.Excel.Corpus"
	worksheets = {}
	for sheet in messy["sheets"]:
		if sheet["kind"] != "worksheet":
			continue
		ws = workbook.create_sheet(sheet["name"])
		worksheets[sheet["name"]] = ws
		for row, row_cells in cells(sheet, {"formula", "table"}):
			for cell in row_cells:
				target = ws[cell["column"] + str(row)]
				if cell["type"] == "Text":
					target.value = cell["text"]
					target.data_type = "s"
				elif cell["type"] == "Number":
					target.value = cell["number"]
				elif cell["type"] == "Formula":
					target.value = "=" + cell["formula"]
		for merge in sheet.get("merges", []):
			ws.merge_cells(merge)
		for column in sheet.get("hiddenColumns", []):
			ws.column_dimensions[column].hidden = True
		for row in sheet.get("hiddenRows", []):
			ws.row_dimensions[row].hidden = True
		for group in sheet.get("groups", []):
			ws.row_dimensions.group(group["first"], group["last"], outline_level=1, hidden=True)
			ws.row_dimensions[group["last"] + 1].collapsed = True
		for table in sheet.get("tables", []):
			names = table_names(sheet, table)
			columns = []
			for i, name in enumerate(names):
				column = TableColumn(id=i + 1, name=name)
				if table.get("totalsLabel") and i == 0:
					column.totalsRowLabel = table["totalsLabel"]
				if table.get("totalsSum") == name:
					column.totalsRowFunction = "sum"
				columns.append(column)
			added = Table(displayName=table["name"], ref=table["range"], tableColumns=columns)
			added.tableStyleInfo = TableStyleInfo(name="TableStyleMedium2", showRowStripes=True)
			if table.get("hideHeader"):
				added.headerRowCount = 0
			else:
				first, last = table["range"].split(":")
				last_column, last_row = split(last)
				data_end = last_row - (1 if table.get("totalsLabel") else 0)
				added.autoFilter = AutoFilter(ref=first + ":" + last_column + str(data_end))
			if table.get("totalsLabel"):
				added.totalsRowCount = 1
			ws.add_table(added)
		spec = sheet.get("filter")
		if spec:
			ws.auto_filter.ref = spec["range"]
			custom = CustomFilters(customFilter=[CustomFilter(operator="notEqual", val=spec["hides"])])
			ws.auto_filter.filterColumn.append(FilterColumn(colId=spec["column"] - 1, customFilters=custom))
			ws.sheet_properties.filterMode = True
			for row in spec["hiddenRows"]:
				ws.row_dimensions[row].hidden = True
		if sheet.get("hideRowsFrom"):
			ws.sheet_format.zeroHeight = True
		if sheet.get("hideColumnsFrom"):
			ws.column_dimensions.group(sheet["hideColumnsFrom"], "XFD", outline_level=0, hidden=True)
	for name in messy["names"]:
		scope = worksheets[name["sheet"]] if name.get("sheet") else None
		target = name["refersTo"]
		if name["name"] == "Print_Area":
			scope.print_area = target.split("!")[1].replace("$", "")
		elif name["name"] == "Print_Titles":
			scope.print_title_rows = target.split("!")[1].replace("$", "")
		else:
			defined = DefinedName(name["name"], attr_text=target, hidden=name.get("hidden"))
			(scope.defined_names if scope is not None else workbook.defined_names)[name["name"]] = defined
	chart_index = next(i for i, s in enumerate(messy["sheets"]) if s["kind"] == "chartsheet")
	chart_spec = messy["sheets"][chart_index]
	chartsheet = workbook.create_chartsheet(chart_spec["name"], chart_index)
	source, area = chart_spec["chart"].split("!")
	first, last = area.split(":")
	first_column, first_row = split(first)
	_, last_row = split(last)
	chart = BarChart()
	chart.add_data(Reference(worksheets[source], min_col=index(first_column), min_row=first_row, max_row=last_row), titles_from_data=True)
	chartsheet.add_chart(chart)
	workbook.save(path)
	save(path)


def write_messy_xlsxwriter(messy, folder):
	"""xlsxwriter/messy.xlsx: merged ranges (merge_range, which writes no cell under a range but its first, without a
	format), hidden rows and columns, a collapsed group, the tables (add_table writes their header and totals rows), a
	filter whose rows it hides itself, hide_unused_rows (zeroHeight) and the columns from D hidden, the chartsheet, and
	every defined name but the hidden one: define_name can't hide a name. Formulas have the results Excel computes."""
	path = os.path.join(folder, "messy.xlsx")
	workbook = xlsxwriter.Workbook(path)
	workbook.set_properties({"author": "CsvHelper.Excel.Corpus"})
	worksheets = {}
	for sheet in messy["sheets"]:
		if sheet["kind"] == "chartsheet":
			chartsheet = workbook.add_chartsheet(sheet["name"])
			source, area = sheet["chart"].split("!")
			first, last = area.split(":")
			first_column, first_row = split(first)
			last_column, last_row = split(last)
			chart = workbook.add_chart({"type": "column"})
			chart.add_series({
				"name": [source, first_row - 1, index(first_column) - 1],
				"values": [source, first_row, index(first_column) - 1, last_row - 1, index(last_column) - 1],
			})
			chartsheet.set_chart(chart)
			continue
		ws = workbook.add_worksheet(sheet["name"])
		worksheets[sheet["name"]] = ws
		tops = merged_values(sheet)
		values = {}
		totals = []
		for table in sheet.get("tables", []):
			if table.get("totalsLabel"):
				first, last = table["range"].split(":")
				totals.append((split(last)[1], split(first)[0] + str(split(last)[1]) + ":" + last))
		for row, row_cells in cells(sheet, {"formula", "table"}):
			for cell in row_cells:
				at = cell["column"] + str(row)
				values[at] = cell
				# merge_range writes a range's first cell, and add_table a totals row.
				if at in tops or any(row == total_row and in_range(at, area) for total_row, area in totals):
					continue
				write_xlsxwriter_cell(ws, at, cell)
		for merge in sheet.get("merges", []):
			cell = values.get(merge.split(":")[0])
			ws.merge_range(merge, (cell.get("text") if cell.get("type") == "Text" else cell.get("number")) if cell else "", None)
		for table in sheet.get("tables", []):
			names = table_names(sheet, table)
			first, last = table["range"].split(":")
			columns = []
			for i, name in enumerate(names):
				column = {"header": name}
				if table.get("totalsLabel") and i == 0:
					column["total_string"] = table["totalsLabel"]
				if table.get("totalsSum") == name:
					column["total_function"] = "sum"
					column["total_value"] = values[letters(index(split(first)[0]) + i) + str(split(last)[1])]["result"]
				columns.append(column)
			ws.add_table(table["range"], {
				"name": table["name"], "columns": columns, "total_row": bool(table.get("totalsLabel")),
				"header_row": not table.get("hideHeader"),
			})
		for column in sheet.get("hiddenColumns", []):
			ws.set_column(column + ":" + column, None, None, {"hidden": True})
		hidden = set(sheet.get("hiddenRows", []))
		spec = sheet.get("filter")
		if spec:
			ws.autofilter(spec["range"])
			ws.filter_column(spec["column"] - 1, "x != " + spec["hides"])
			hidden.update(spec["hiddenRows"])
		for row in sorted(hidden):
			ws.set_row(row - 1, None, None, {"hidden": True})
		for group in sheet.get("groups", []):
			for row in range(group["first"], group["last"] + 1):
				ws.set_row(row - 1, None, None, {"level": 1, "hidden": True})
			ws.set_row(group["last"], None, None, {"collapsed": True})
		if sheet.get("hideRowsFrom"):
			ws.set_default_row(hide_unused_rows=True)
		if sheet.get("hideColumnsFrom"):
			ws.set_column(sheet["hideColumnsFrom"] + ":XFD", None, None, {"hidden": True})
	for name in messy["names"]:
		target = name["refersTo"]
		if name.get("hidden"):
			continue
		if name["name"] == "Print_Area":
			worksheets[name["sheet"]].print_area(target.split("!")[1].replace("$", ""))
		elif name["name"] == "Print_Titles":
			first, last = target.split("!")[1].replace("$", "").split(":")
			worksheets[name["sheet"]].repeat_rows(int(first) - 1, int(last) - 1)
		else:
			workbook.define_name((name["sheet"] + "!" if name.get("sheet") else "") + name["name"], "=" + target)
	workbook.close()
	save(path)


def write_xlsxwriter_cell(ws, at, cell):
	kind = cell["type"]
	if kind == "Text":
		ws.write_string(at, cell["text"])
	elif kind == "Number":
		ws.write_number(at, cell["number"])
	elif kind == "Formula":
		ws.write_formula(at, "=" + cell["formula"], None, cell["result"])


def in_range(at, cells_range):
	first, last = cells_range.split(":")
	column, row = split(at)
	first_column, first_row = split(first)
	last_column, last_row = split(last)
	return first_row <= row <= last_row and (len(first_column), first_column) <= (len(column), column) <= (len(last_column), last_column)


def split(reference):
	letters = "".join(c for c in reference if c.isalpha())
	return letters, int(reference[len(letters):])


def folder(root, name):
	path = os.path.join(root, name)
	os.makedirs(path, exist_ok=True)
	return path


def main():
	with open(sys.argv[1], encoding="utf-8") as file:
		spec = json.load(file)
	with open(sys.argv[2], encoding="utf-8") as file:
		typed = json.load(file)
	with open(sys.argv[3], encoding="utf-8") as file:
		messy = json.load(file)
	root = sys.argv[4]
	write_openpyxl(spec, folder(root, "openpyxl"))
	write_xlsxwriter(spec, folder(root, "xlsxwriter"), "basic.xlsx", {})
	write_xlsxwriter(spec, folder(root, "xlsxwriter"), "constant_memory.xlsx", {"constant_memory": True})
	write_pandas(spec, folder(root, "pandas"))
	write_typed_openpyxl(typed, folder(root, "openpyxl"), "typed.xlsx", False)
	write_typed_openpyxl(typed, folder(root, "openpyxl"), "typed-isodates.xlsx", True)
	write_typed_xlsxwriter(typed, folder(root, "xlsxwriter"))
	write_messy_openpyxl(messy, folder(root, "openpyxl"))
	write_messy_xlsxwriter(messy, folder(root, "xlsxwriter"))


if __name__ == "__main__":
	main()

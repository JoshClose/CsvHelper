"""Writes the corpus's Python files from the basic workbook's JSON: python -I corpus.py <spec.json> <Files folder>.

openpyxl/basic.xlsx, xlsxwriter/basic.xlsx, xlsxwriter/constant_memory.xlsx, and pandas/openpyxl.xlsx and
pandas/xlsxwriter.xlsx (each engine named, since pandas picks XlsxWriter when it is installed). Each writes what its
library can: openpyxl can't write a control character (it raises IllegalCharacterError) and writes formulas without
cached results; XlsxWriter writes the results Excel computes; pandas writes a data frame per sheet.
"""

import datetime
import json
import os
import sys

import openpyxl
import pandas
import xlsxwriter
from openpyxl.cell.rich_text import CellRichText, TextBlock
from openpyxl.cell.text import InlineFont
from openpyxl.chart import BarChart, Reference

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


def folder(root, name):
	path = os.path.join(root, name)
	os.makedirs(path, exist_ok=True)
	return path


def main():
	with open(sys.argv[1], encoding="utf-8") as file:
		spec = json.load(file)
	root = sys.argv[2]
	write_openpyxl(spec, folder(root, "openpyxl"))
	write_xlsxwriter(spec, folder(root, "xlsxwriter"), "basic.xlsx", {})
	write_xlsxwriter(spec, folder(root, "xlsxwriter"), "constant_memory.xlsx", {"constant_memory": True})
	write_pandas(spec, folder(root, "pandas"))


if __name__ == "__main__":
	main()

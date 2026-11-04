import java.io.FileOutputStream;
import java.io.IOException;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

import org.apache.poi.ss.formula.FormulaParseException;
import org.apache.poi.ss.usermodel.Cell;
import org.apache.poi.ss.usermodel.CellStyle;
import org.apache.poi.ss.usermodel.Font;
import org.apache.poi.ss.usermodel.FormulaError;
import org.apache.poi.ss.usermodel.RichTextString;
import org.apache.poi.ss.usermodel.Row;
import org.apache.poi.ss.usermodel.Sheet;
import org.apache.poi.ss.usermodel.SheetVisibility;
import org.apache.poi.ss.usermodel.Workbook;
import org.apache.poi.ss.util.CellRangeAddress;
import org.apache.poi.ss.util.CellReference;
import org.apache.poi.xssf.streaming.SXSSFFormulaEvaluator;
import org.apache.poi.xssf.streaming.SXSSFWorkbook;
import org.apache.poi.xssf.usermodel.XSSFFormulaEvaluator;
import org.apache.poi.xssf.usermodel.XSSFWorkbook;

/**
 * Writes the corpus's Apache POI files from the basic and typed workbooks' JSON:
 * {@code java -cp <jars> Corpus.java <basic.json> <typed.json> <Files folder>}.
 *
 * <p>{@code poi/xssf.xlsx} comes from XSSF, which holds the workbook in memory and writes shared strings, and
 * {@code poi/sxssf.xlsx} from SXSSF, its streaming writer, which writes inline strings by default. Each writes what POI
 * can: it can't create a chartsheet or a phonetic run, and SXSSF writes a rich text's runs as plain text. The formulas
 * get their results from POI's own evaluator. {@code poi/typed.xlsx} comes from XSSF: built-in format ids, error
 * constants, formulas and a legacy array, without the dynamic arrays and the table POI can't write. A formula POI's
 * parser refuses is left out, and one its evaluator can't compute keeps no result.</p>
 */
public final class Corpus
{
	/** The features of the basic workbook's cells that POI writes. */
	private static final Set<String> FEATURES = Set.of("control", "long", "rich", "formula", "date");

	/** The features of the typed workbook's cells that POI writes. */
	private static final Set<String> TYPED_FEATURES = Set.of("formatId", "error", "formula", "array", "name", "newer", "date");

	/** Not created: the class only has its entry point. */
	private Corpus()
	{
	}

	/**
	 * Writes both files.
	 *
	 * @param args the basic workbook's spec, the typed workbook's, and the corpus folder
	 * @throws IOException when a file can't be read or written
	 */
	public static void main(String[] args) throws IOException
	{
		var spec = asList(new Json(Files.readString(Path.of(args[0]), StandardCharsets.UTF_8)).read());
		var typed = asMap(new Json(Files.readString(Path.of(args[1]), StandardCharsets.UTF_8)).read());
		var folder = Files.createDirectories(Path.of(args[2], "poi"));

		try (var workbook = new XSSFWorkbook())
		{
			write(spec, workbook, workbook);
			XSSFFormulaEvaluator.evaluateAllFormulaCells(workbook);
			save(workbook, folder.resolve("xssf.xlsx"));
		}

		// SXSSF keeps a window of rows (100 by default) in memory, which holds every row of the basic workbook, so its
		// evaluator can reach every formula.
		var inner = new XSSFWorkbook();
		try (var workbook = new SXSSFWorkbook(inner))
		{
			write(spec, workbook, inner);
			SXSSFFormulaEvaluator.evaluateAllFormulaCells(workbook, false);
			save(workbook, folder.resolve("sxssf.xlsx"));
		}

		try (var workbook = new XSSFWorkbook())
		{
			writeTyped(typed, workbook);
			save(workbook, folder.resolve("typed.xlsx"));
		}
	}

	/**
	 * Writes the typed workbook's sheets and names into a workbook, then evaluates each formula on its own.
	 *
	 * @param typed the typed workbook's spec: its sheets and names
	 * @param workbook the workbook to write
	 */
	private static void writeTyped(Map<String, Object> typed, XSSFWorkbook workbook)
	{
		workbook.getProperties().getCoreProperties().setCreator("CsvHelper.Excel.Corpus");
		for (var entry : asMap(typed.get("names")).entrySet())
		{
			var name = workbook.createName();
			name.setNameName(entry.getKey());
			name.setRefersToFormula((String)entry.getValue());
		}

		// Every sheet first, so that a formula can refer to one after its own.
		for (var item : asList(typed.get("sheets")))
		{
			workbook.createSheet((String)asMap(item).get("name"));
		}

		var styles = new HashMap<String, CellStyle>();
		var formulas = new ArrayList<Cell>();
		for (var item : asList(typed.get("sheets")))
		{
			var sheetSpec = asMap(item);
			var sheet = workbook.getSheet((String)sheetSpec.get("name"));
			for (var rowItem : asList(sheetSpec.get("rows")))
			{
				var rowSpec = asMap(rowItem);
				// A row an array formula made already holds its cells, which createRow would replace.
				var index = ((Double)rowSpec.get("row")).intValue() - 1;
				var row = sheet.getRow(index) != null ? sheet.getRow(index) : sheet.createRow(index);
				for (var cellItem : asList(rowSpec.get("cells")))
				{
					var cellSpec = asMap(cellItem);
					var feature = (String)cellSpec.get("feature");
					if (feature == null || TYPED_FEATURES.contains(feature))
					{
						writeTypedCell(workbook, sheet, row, cellSpec, styles, formulas);
					}
				}
			}
		}

		var evaluator = workbook.getCreationHelper().createFormulaEvaluator();
		for (var cell : formulas)
		{
			try
			{
				evaluator.evaluateFormulaCell(cell);
			}
			catch (RuntimeException e)
			{
				System.out.println("  no result for " + cell.getSheet().getSheetName() + "!" + cell.getAddress() + ", which POI can't evaluate: " + e.getMessage());
			}
		}
	}

	/**
	 * Writes a cell of the typed workbook: a value, an error constant, a number with its format, or a formula, which is
	 * left out when POI's parser refuses it.
	 *
	 * @param workbook the workbook
	 * @param sheet the cell's sheet
	 * @param row the cell's row
	 * @param spec the cell's spec
	 * @param styles the styles made so far, by format id or code
	 * @param formulas the formula cells written so far, to evaluate
	 */
	private static void writeTypedCell(Workbook workbook, Sheet sheet, Row row, Map<String, Object> spec, Map<String, CellStyle> styles, List<Cell> formulas)
	{
		var cell = row.createCell(CellReference.convertColStringToIndex((String)spec.get("column")));
		switch ((String)spec.get("type"))
		{
			case "Text" -> cell.setCellValue((String)spec.get("text"));
			case "Number" -> cell.setCellValue((Double)spec.get("number"));
			case "Bool" -> cell.setCellValue((Boolean)spec.get("bool"));
			case "Error" -> cell.setCellErrorValue(FormulaError.forString((String)spec.get("text")).getCode());
			case "Date" -> writeTypedDate(workbook, cell, spec, styles);
			case "Formula" ->
			{
				var formula = (String)spec.get("formula");
				try
				{
					var array = (String)spec.get("array");
					if (array != null)
					{
						sheet.setArrayFormula(formula, CellRangeAddress.valueOf(array));
					}
					else
					{
						cell.setCellFormula(formula);
					}

					formulas.add(cell);
				}
				catch (FormulaParseException e)
				{
					row.removeCell(cell);
					System.out.println("  left out " + sheet.getSheetName() + "!" + cell.getAddress() + ", which POI can't parse: " + formula);
				}
			}
			default -> throw new IllegalArgumentException("The cell type " + spec.get("type") + " is not one POI writes.");
		}
	}

	/**
	 * Writes a number of the typed workbook with its format, a built-in format id or a code, or a date as a date.
	 *
	 * @param workbook the workbook
	 * @param cell the cell
	 * @param spec the cell's spec
	 * @param styles the styles made so far, by format id or code
	 */
	private static void writeTypedDate(Workbook workbook, Cell cell, Map<String, Object> spec, Map<String, CellStyle> styles)
	{
		var id = (Double)spec.get("formatId");
		var key = id != null ? "id " + id.intValue() : (String)spec.get("format");
		var style = styles.computeIfAbsent(key, k ->
		{
			var created = workbook.createCellStyle();
			created.setDataFormat(id != null ? (short)id.intValue() : workbook.getCreationHelper().createDataFormat().getFormat((String)spec.get("format")));

			return created;
		});
		cell.setCellStyle(style);
		var date = (String)spec.get("date");
		if (date != null)
		{
			cell.setCellValue(LocalDateTime.parse(date));
		}
		else
		{
			cell.setCellValue((Double)spec.get("number"));
		}
	}

	/**
	 * Writes the basic workbook's worksheets into a workbook.
	 *
	 * @param spec the basic workbook's sheets
	 * @param workbook the workbook to write
	 * @param properties the XSSF workbook that holds the document's properties (the workbook itself, or SXSSF's)
	 */
	private static void write(List<Object> spec, Workbook workbook, XSSFWorkbook properties)
	{
		properties.getProperties().getCoreProperties().setCreator("CsvHelper.Excel.Corpus");
		var bold = workbook.createFont();
		bold.setBold(true);
		var styles = new HashMap<String, CellStyle>();
		for (var item : spec)
		{
			var sheetSpec = asMap(item);
			if (!"worksheet".equals(sheetSpec.get("kind")))
			{
				continue;
			}

			var sheet = workbook.createSheet((String)sheetSpec.get("name"));
			var index = workbook.getSheetIndex(sheet);
			switch ((String)sheetSpec.get("state"))
			{
				case "hidden" -> workbook.setSheetVisibility(index, SheetVisibility.HIDDEN);
				case "veryHidden" -> workbook.setSheetVisibility(index, SheetVisibility.VERY_HIDDEN);
				default ->
				{
				}
			}

			for (var rowItem : asList(sheetSpec.get("rows")))
			{
				var rowSpec = asMap(rowItem);
				var row = sheet.createRow(((Double)rowSpec.get("row")).intValue() - 1);
				for (var cellItem : asList(rowSpec.get("cells")))
				{
					var cellSpec = asMap(cellItem);
					var feature = (String)cellSpec.get("feature");
					if (feature == null || FEATURES.contains(feature))
					{
						writeCell(workbook, row, cellSpec, bold, styles);
					}
				}
			}
		}
	}

	/**
	 * Writes a cell.
	 *
	 * @param workbook the workbook
	 * @param row the cell's row
	 * @param spec the cell's spec
	 * @param bold the bold font, for rich text
	 * @param styles the date styles made so far, by format code
	 */
	private static void writeCell(Workbook workbook, Row row, Map<String, Object> spec, Font bold, Map<String, CellStyle> styles)
	{
		var cell = row.createCell(CellReference.convertColStringToIndex((String)spec.get("column")));
		switch ((String)spec.get("type"))
		{
			case "Text" -> cell.setCellValue((String)spec.get("text"));
			case "Number" -> cell.setCellValue((Double)spec.get("number"));
			case "Bool" -> cell.setCellValue((Boolean)spec.get("bool"));
			case "Formula" -> cell.setCellFormula((String)spec.get("formula"));
			case "Date" -> writeDate(workbook, cell, spec, styles);
			case "Rich" -> cell.setCellValue(richText(workbook, asList(spec.get("runs")), bold));
			default -> throw new IllegalArgumentException("The cell type " + spec.get("type") + " is not one POI writes.");
		}
	}

	/**
	 * Writes a date, or a time alone as its serial, in its format.
	 *
	 * @param workbook the workbook
	 * @param cell the cell
	 * @param spec the cell's spec
	 * @param styles the date styles made so far, by format code
	 */
	private static void writeDate(Workbook workbook, Cell cell, Map<String, Object> spec, Map<String, CellStyle> styles)
	{
		var format = (String)spec.get("format");
		var style = styles.computeIfAbsent(format, code ->
		{
			var created = workbook.createCellStyle();
			created.setDataFormat(workbook.getCreationHelper().createDataFormat().getFormat(code));

			return created;
		});
		cell.setCellStyle(style);
		var date = (String)spec.get("date");
		if (date != null)
		{
			cell.setCellValue(LocalDateTime.parse(date));
		}
		else
		{
			cell.setCellValue((Double)spec.get("number"));
		}
	}

	/**
	 * Makes a rich text from its runs, the bold ones in the bold font.
	 *
	 * @param workbook the workbook
	 * @param runs the runs' specs
	 * @param bold the bold font
	 * @return the rich text
	 */
	private static RichTextString richText(Workbook workbook, List<Object> runs, Font bold)
	{
		var text = new StringBuilder();
		for (var run : runs)
		{
			text.append((String)asMap(run).get("text"));
		}

		var rich = workbook.getCreationHelper().createRichTextString(text.toString());
		var start = 0;
		for (var run : runs)
		{
			var runSpec = asMap(run);
			var end = start + ((String)runSpec.get("text")).length();
			if (Boolean.TRUE.equals(runSpec.get("bold")))
			{
				rich.applyFont(start, end, bold);
			}

			start = end;
		}

		return rich;
	}

	/**
	 * Saves a workbook and prints its path.
	 *
	 * @param workbook the workbook
	 * @param path the file
	 * @throws IOException when the file can't be written
	 */
	private static void save(Workbook workbook, Path path) throws IOException
	{
		try (OutputStream stream = new FileOutputStream(path.toFile()))
		{
			workbook.write(stream);
		}

		System.out.println("  " + path);
	}

	/**
	 * A JSON value as an object.
	 *
	 * @param value the value
	 * @return the object
	 */
	@SuppressWarnings("unchecked")
	private static Map<String, Object> asMap(Object value)
	{
		return (Map<String, Object>)value;
	}

	/**
	 * A JSON value as an array.
	 *
	 * @param value the value
	 * @return the array
	 */
	@SuppressWarnings("unchecked")
	private static List<Object> asList(Object value)
	{
		return (List<Object>)value;
	}

	/**
	 * A reader of the JSON the corpus tool's {@code spec} writes: objects as maps in order, arrays as lists, numbers as
	 * doubles, and strings, booleans and null. Java has no JSON reader of its own, and the spec is small.
	 */
	private static final class Json
	{
		/** The JSON text. */
		private final String text;

		/** The position of the next character to read. */
		private int position;

		/**
		 * Creates a reader of a JSON text.
		 *
		 * @param text the text
		 */
		private Json(String text)
		{
			this.text = text;
		}

		/**
		 * Reads the text's value, which must be all of it.
		 *
		 * @return the value
		 */
		private Object read()
		{
			var value = value();
			skipWhitespace();
			if (position != text.length())
			{
				throw error("text after the value");
			}

			return value;
		}

		/**
		 * Reads a value.
		 *
		 * @return the value
		 */
		private Object value()
		{
			skipWhitespace();
			if (position == text.length())
			{
				throw error("the end of the text");
			}

			var c = text.charAt(position);
			if (c == '{')
			{
				return object();
			}

			if (c == '[')
			{
				return array();
			}

			if (c == '"')
			{
				return string();
			}

			if (text.startsWith("true", position))
			{
				position += 4;

				return Boolean.TRUE;
			}

			if (text.startsWith("false", position))
			{
				position += 5;

				return Boolean.FALSE;
			}

			if (text.startsWith("null", position))
			{
				position += 4;

				return null;
			}

			return number();
		}

		/**
		 * Reads an object.
		 *
		 * @return its members, in order
		 */
		private Map<String, Object> object()
		{
			var members = new LinkedHashMap<String, Object>();
			position++;
			skipWhitespace();
			if (text.charAt(position) == '}')
			{
				position++;

				return members;
			}

			while (true)
			{
				skipWhitespace();
				var name = string();
				skipWhitespace();
				expect(':');
				members.put(name, value());
				skipWhitespace();
				if (text.charAt(position) == '}')
				{
					position++;

					return members;
				}

				expect(',');
			}
		}

		/**
		 * Reads an array.
		 *
		 * @return its items
		 */
		private List<Object> array()
		{
			var items = new ArrayList<Object>();
			position++;
			skipWhitespace();
			if (text.charAt(position) == ']')
			{
				position++;

				return items;
			}

			while (true)
			{
				items.add(value());
				skipWhitespace();
				if (text.charAt(position) == ']')
				{
					position++;

					return items;
				}

				expect(',');
			}
		}

		/**
		 * Reads a string, decoding its escapes; a surrogate pair's two escapes become the pair.
		 *
		 * @return the string
		 */
		private String string()
		{
			expect('"');
			var result = new StringBuilder();
			while (true)
			{
				var c = text.charAt(position++);
				if (c == '"')
				{
					return result.toString();
				}

				if (c != '\\')
				{
					result.append(c);

					continue;
				}

				var escape = text.charAt(position++);
				switch (escape)
				{
					case '"', '\\', '/' -> result.append(escape);
					case 'b' -> result.append('\b');
					case 'f' -> result.append('\f');
					case 'n' -> result.append('\n');
					case 'r' -> result.append('\r');
					case 't' -> result.append('\t');
					case 'u' ->
					{
						result.append((char)Integer.parseInt(text.substring(position, position + 4), 16));
						position += 4;
					}
					default -> throw error("the escape \\" + escape);
				}
			}
		}

		/**
		 * Reads a number.
		 *
		 * @return the number
		 */
		private Double number()
		{
			var start = position;
			while (position < text.length() && "+-0123456789.eE".indexOf(text.charAt(position)) >= 0)
			{
				position++;
			}

			if (start == position)
			{
				throw error("the character '" + text.charAt(position) + "'");
			}

			return Double.valueOf(text.substring(start, position));
		}

		/**
		 * Reads a character that must come next.
		 *
		 * @param c the character
		 */
		private void expect(char c)
		{
			if (position >= text.length() || text.charAt(position) != c)
			{
				throw error("a character other than '" + c + "'");
			}

			position++;
		}

		/** Skips JSON white space. */
		private void skipWhitespace()
		{
			while (position < text.length() && " \t\r\n".indexOf(text.charAt(position)) >= 0)
			{
				position++;
			}
		}

		/**
		 * An error at the current position.
		 *
		 * @param found what was found there
		 * @return the error
		 */
		private IllegalArgumentException error(String found)
		{
			return new IllegalArgumentException("The spec's JSON has " + found + " at character " + position + ".");
		}
	}
}

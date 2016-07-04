using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using OfficeOpenXml;

namespace MeasureDegredation
{
	public static class SpreadsheetWriter
	{
		public static void WriteSpreadsheet(string filename, Dictionary<string, IEnumerable<ComparisonAgregator>> comparisonsByName)
		{
			// Example at http://epplus.codeplex.com/wikipage?title=ContentSheetExample

			var fileInfo = new FileInfo(filename);

			if (fileInfo.Exists)
				fileInfo.Delete();

			var pck = new ExcelPackage(fileInfo);

			foreach (var kvp in comparisonsByName)
			{
				var name = kvp.Key;
				var comparisons = kvp.Value;

				var worksheet = pck.Workbook.Worksheets.Add(name);

				worksheet.Cells[1,2].Value = "Worst Error";
				worksheet.Cells[1,3].Value = "Worst Error (16 bit)";
				worksheet.Cells[1,4].Value = "Worst bits per sample";
				worksheet.Cells[1,5].Value = "Worst Equilization";
				worksheet.Cells[1,6].Value = "Average Error";
				worksheet.Cells[1,7].Value = "Average Error (16 bit)";
				worksheet.Cells[1,8].Value = "Average bits per sample";
				worksheet.Cells[1,9].Value = "Equilization";

				var rowCtr = 2;
				foreach (var comparison in comparisons.OrderBy(c => c.SortOrder))
				{
					worksheet.Cells[rowCtr, 1].Value = comparison.DisplayTitle;
					worksheet.Cells[rowCtr, 2].Value = comparison.LargestError;
					worksheet.Cells[rowCtr, 3].Value = comparison.LargestError * short.MaxValue;
					worksheet.Cells[rowCtr, 4].Value = comparison.WorstBits;
					worksheet.Cells[rowCtr, 5].Value = comparison.LargestEquilization * 100;
					worksheet.Cells[rowCtr, 6].Value = comparison.AverageError;
					worksheet.Cells[rowCtr, 7].Value = comparison.AverageError * short.MaxValue;
					worksheet.Cells[rowCtr, 8].Value = comparison.AverageBits;
					worksheet.Cells[rowCtr, 9].Value = comparison.Equilization * 100;

					rowCtr++;
				}

				for (var ctr = 1; ctr <= 9; ctr++)
				{
					worksheet.Column(ctr).AutoFit();
				}

				//CreateChart(worksheet);
			}

			pck.Save();
		}

		/*public static void CreateChart(ExcelWorksheet worksheet)
		{
			worksheet.Drawings.Clear();

			var chart = worksheet.Drawings.AddChart(
				"Bits per sample per frequency",
				OfficeOpenXml.Drawing.Chart.eChartType.BarClustered);
			
			chart.Title.Text = "Bits per sample per frequency";
			chart.SetPosition(
				Row: 1,
				RowOffsetPixels: 0,
				Column: 10,
				ColumnOffsetPixels: 0);

			//chart.SetSize(800, 300);
			var worstBitSeries = chart.Series.Add("0:0", "D:D");
			//chart.Series.Add("H2:D", "A2:A");
			//series1.Header = "header1";
			//var series2 = chart.Series.Add(worksheet.Cells["H1:D10000"], worksheet.Cells["H1:D10000"]);
			//series2.Header = "header1";
		}*/
	}
}


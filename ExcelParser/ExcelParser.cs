﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ExcelParser.Models;
using Excel = Microsoft.Office.Interop.Excel;

namespace ExcelParser
{
    internal class ExcelParser
    {
        private readonly string _filePath;
        /// <summary>
        /// Показывать прогресс каждые _numberRowsForProgress строк
        /// </summary>
        private readonly int _rowsForProgressCount = 100;
        /// <summary>
        /// Количество обработанных строк
        /// </summary>
        private int _rowsExecutedCount;
        /// <summary>
        /// Количество логических процессоров на локальной машине
        /// </summary>
        private int _processorCount;
        /// <summary>
        /// Найденные колонки в файле, которые соответствуют необходимым
        /// </summary>
        private Dictionary<string, int> _foundRequiredColumns = new Dictionary<string, int>();
        /// <summary>
        /// Найденные колонки для установления значений в файле, которые соответствуют необходимым
        /// </summary>
        private Dictionary<string, int> _foundColumnsForSettings = new Dictionary<string, int>();

        public ExcelParser(string filePath)
        {
            this._filePath = filePath;
        }

        public void Start()
        {
            Console.WriteLine($"Работа с файлом {_filePath}\n" +
                $"Чтобы запустить работу нажмите \"Enter\"");
            Console.ReadLine();

            _processorCount = Environment.ProcessorCount;
            StartParser();
        }
        private void StartParser()
        {
            Console.WriteLine($"Работа с файлом {_filePath} началась!");
            bool showProgress = true;

            try
            {
                using (ExcelHelper helper = new ExcelHelper())
                {
                    if (helper.Open(filePath: Path.Combine(Environment.CurrentDirectory, _filePath)))
                    {
                        Excel.Sheets sheets = helper.Workbook.Sheets;

                        foreach (Excel.Worksheet worksheet in sheets)
                        {
                            Excel.Range usedRange = worksheet.UsedRange; // Получаем диапазон используемых на странице ячеек        
                            Excel.Range rows = usedRange.Rows; // Получаем строки в используемом диапазоне
                            Excel.Range colums = usedRange.Columns; // Получаем столбцы в используемом диапазоне

                            int rowsCount = rows.Count;
                            int columnsCount = colums.Count;
                            //Получение нужных столбцов
                            for (int j = 1; j <= columnsCount; j++)
                            {
                                Excel.Range cellRange = usedRange.Cells[1, j]; //row: 1 column: j
                                string cellText = (cellRange == null || cellRange.Value2 == null) ? null :
                                                    (cellRange as Excel.Range).Value2.ToString();

                                if (Columns.RequiredColumns.Contains(cellText)) { _foundRequiredColumns.Add(cellText, j); }
                                if (Columns.ColumnsForSettings.Contains(cellText)) { _foundColumnsForSettings.Add(cellText, j); }
                            }

                            StatusColumns();
                            PrintMessage.PrintSuccessMessage($"Началась обработка! Необходимо обработать: {rowsCount} строк. Выделено потоков: {_processorCount}");
                            
                            Stopwatch watchTimer = new Stopwatch();
                            watchTimer.Start();
                            var options = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount - 1 };
                            
                            Parallel.For(2, rowsCount + 1, options, i =>
                            {
                                Activitie activitie = new Activitie(helper, _foundColumnsForSettings, i);

                                foreach (var column in _foundRequiredColumns)
                                {
                                    Excel.Range cellRange = usedRange.Cells[i, column.Value];
                                    string cellText = (cellRange == null || cellRange.Value2 == null) ? null :
                                                        (cellRange as Excel.Range).Value2.ToString();

                                    activitie.CheckActivities(column.Key, cellText);
                                }
                                activitie.StartSettings();

                                #region PROGRESS BAR
                                if (i % _rowsForProgressCount == 0)
                                {
                                    _rowsExecutedCount += _rowsForProgressCount;
                                    Console.WriteLine($"Поток: #{Thread.CurrentThread.ManagedThreadId}. Прогресс: {_rowsExecutedCount}/{rowsCount} ");
                                }
                               
                                if (showProgress && _rowsExecutedCount == 1000)
                                {
                                    TimeSpan tempTimeSpan = watchTimer.Elapsed;
                                    double coeff = rowsCount / 1000;
                                    var ts = TimeSpan.FromSeconds(tempTimeSpan.TotalSeconds * coeff);

                                    PrintMessage.PrintWarningMessage($"Используемые потоки завершили 1000 строк за {tempTimeSpan.Minutes}m. {tempTimeSpan.Seconds}s.\n" +
                                    $"Примерное время завершения потоков: {ts.Minutes}m. {ts.Seconds}s.");
                                    showProgress = false;
                                }
                                #endregion
                            });
                            #region PROGRESS BAR ENDTIME
                            TimeSpan timeSpan = watchTimer.Elapsed;
                            PrintMessage.PrintSuccessMessage($"Работа завершена. " +
                                $"Время: {timeSpan.Hours}h {timeSpan.Minutes}m. {timeSpan.Seconds}s. " +
                                $"Установлено значений: {helper.SettingsCount} ячеек.");
                            watchTimer.Stop();
                            #endregion
                        }
                        helper.Save();
                    }
                }
            }
            catch (Exception ex) { PrintMessage.PrintErrorMessage(ex.Message); }
        }
        private void StatusColumns()
        {
            PrintMessage.PrintSuccessMessage($"Найдено обрабатываемых столбцов: {_foundRequiredColumns.Count} из необходимых {Columns.RequiredColumns.Count}");
            PrintMessage.PrintSuccessMessage($"Найдено столбцов для установления значений: {_foundColumnsForSettings.Count} из необходимых {Columns.ColumnsForSettings.Count}");

            if (_foundRequiredColumns.Count != Columns.RequiredColumns.Count)
            {
                PrintMessage.PrintWarningMessage($"ВНИМАНИЕ! Несовпадение найденных обрабатываемых и необходимых столбцов!\n" +
                $"Чтобы продолжить работу нажмите \"Enter\"");
                Console.ReadKey();
            }
            if (_foundColumnsForSettings.Count != Columns.ColumnsForSettings.Count)
            {
                PrintMessage.PrintWarningMessage($"ВНИМАНИЕ! Несовпадение найденных столбцов для установления значений и необходимых столбцов!\n" +
                $"Чтобы продолжить работу нажмите \"Enter\"");
                Console.ReadKey();
            }
        }
    }
}

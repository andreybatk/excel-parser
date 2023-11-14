﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        private Dictionary<string, int> _foundColumns;
        /// <summary>
        /// Необходимые колонки
        /// </summary>
        private List<string> _requiredColumns = new List<string>
        {   "Катег. защитн.",
            "Катег. Земель",
            "Мер. 1",
            "% выборки",
            "Группа А",
            "Класс А",
            "Преобл. Порода",
            "Бонитет",
            "ТЛУ",
            "A1",
            "Полнота1",
            "Запас1",
            "Густота подр."
        };
        //private static List<int> _currentNumbersColumns = new List<int> { 4, 10, 18, 19, 26, 27, 28, 30, 32, 44, 49, 51, 151 };
        
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

            Excel.Sheets sheets;
            _foundColumns = new Dictionary<string, int>();
            bool showProgress = true;
            try
            {
                using (ExcelHelper helper = new ExcelHelper())
                {
                    if (helper.Open(filePath: Path.Combine(Environment.CurrentDirectory, _filePath)))
                    {
                        sheets = helper.Workbook.Sheets;

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

                                if (_requiredColumns.Contains(cellText)) { _foundColumns.Add(cellText, j); }
                            }

                            StatusColumns();
                            PrintMessage.PrintSuccessMessage($"Началась обработка! Необходимо обработать: {rowsCount} строк. Выделено потоков: {_processorCount}");
                            
                            Stopwatch watchTimer = new Stopwatch();
                            watchTimer.Start();
                            var options = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount - 1 };
                            Parallel.For(2, rowsCount, options, i =>
                            {
                                foreach (var column in _foundColumns)
                                {
                                    Excel.Range cellRange = usedRange.Cells[i, column.Value];
                                    string cellText = (cellRange == null || cellRange.Value2 == null) ? null :
                                                        (cellRange as Excel.Range).Value2.ToString();

                                    if (cellText != null)
                                    {
                                        if (column.Key == _requiredColumns[0]) // если столбец под индексом 0
                                        {
                                            //helper.Set(i, 2, data: "MYTEST2"); //устанавливаем значение в нужную строку и колонку (строка автоматический берется и i)
                                        }
                                    }
                                }

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
                            PrintMessage.PrintSuccessMessage($"Работа завершена. Время: {timeSpan.Hours}h {timeSpan.Minutes}m. {timeSpan.Seconds}s.");
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
            PrintMessage.PrintSuccessMessage($"Найдено столбцов: {_foundColumns.Count - 1} из необходимых {_requiredColumns.Count - 1}");

            if (_foundColumns.Count != _requiredColumns.Count)
            {
                PrintMessage.PrintWarningMessage($"ВНИМАНИЕ! Несовпадение найденных и необходимых столбцов!\n" +
                $"Чтобы продолжить работу нажмите \"Enter\"");
                Console.ReadKey();
            }
        }
    }
}

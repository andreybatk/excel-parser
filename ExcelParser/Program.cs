﻿namespace ExcelParser
{
    /*
     TESTS MULTITHREADING:
     Примерное время завершения потоков: 13m. 34s.
     Работа завершена.Время: 0h 13m. 25s.
     TESTS SINGLETHREADING:
     Примерное время завершения потоков: 25m. 12s.
     Закрытие потоков: http://www.hanselman.com/blog/more-tips-from-sairama-catching-ctrlc-from-a-net-console-application
    */

    internal class Program
    {
        private static readonly string _file = "AIS_POL.xlsx";

        static void Main(string[] args)
        {
            ExcelParser parser = new ExcelParser(_file);
            parser.Start();
        }
    }
}



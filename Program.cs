﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MHWSharpnessExtractor.DataSources;

namespace MHWSharpnessExtractor
{
    class Program
    {
        public enum ResultCode
        {
            Success = 0,
            FormatError = 1,
            OtherError = 2
        }

        static int Main(string[] args)
        {
            return new Program().Run(args).Result;
        }

        private async Task<int> Run(string[] args)
        {
            ResultCode resultCode;
            //IDataSource dataSource = new MhwgDotOrg();
            IDataSource dataSource = new MhwDbDotCom();

            try
            {
                IList<Weapon> weapons = await dataSource.ProduceWeaponsAsync();
                resultCode = ResultCode.Success;
            }
            catch (FormatException ex)
            {
                Console.WriteLine(ex.Message);
                resultCode = ResultCode.FormatError;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                resultCode = ResultCode.OtherError;
            }

            return (int)resultCode;
        }
    }
}

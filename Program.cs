using System;
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
            IDataSource sourceDataSource = new MhwgDotOrg();
            IDataSource targetDataSource = new MhwDbDotCom();

            try
            {
                Task<IList<Weapon>> sourceWeaponsTask = sourceDataSource.ProduceWeaponsAsync();
                Task<IList<Weapon>> targetWeaponsTask = targetDataSource.ProduceWeaponsAsync();

                await Task.WhenAll(sourceWeaponsTask, targetWeaponsTask);

                IList<Weapon> sourceWeapons = sourceWeaponsTask.Result;
                IList<Weapon> targetWeapons = targetWeaponsTask.Result;

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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoProcessor.Services
{
    public  class FakeLoadService: IFakeLoadService
    {


        public async Task LoadTest()
        {
            await Task.Delay(120000);
        }
    }
}

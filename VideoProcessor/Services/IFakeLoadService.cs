using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoProcessor.Services
{
    public interface IFakeLoadService
    {
        public Task LoadTest();
    }
}

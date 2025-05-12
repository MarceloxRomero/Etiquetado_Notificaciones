using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Etiquetado_Notificaciones
{
    public class RateLimiter
    {
        private readonly TimeSpan _period;
        private readonly SemaphoreSlim _semaphore;
        private DateTime _lastCallTime = DateTime.MinValue;

        public RateLimiter(int maxCalls, TimeSpan period)
        {
            _period = period;
            _semaphore = new SemaphoreSlim(maxCalls, maxCalls);
        }

        public async Task WaitAsync()
        {
            await _semaphore.WaitAsync();

            try
            {
                var timeSinceLastCall = DateTime.Now - _lastCallTime;
                if (timeSinceLastCall < _period)
                    await Task.Delay(_period - timeSinceLastCall);
            }
            finally
            {
                _lastCallTime = DateTime.Now;
                _semaphore.Release();
            }
        }
    }
}

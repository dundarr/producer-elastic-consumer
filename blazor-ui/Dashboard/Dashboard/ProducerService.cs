using Dashboard;

namespace Producer.Dashboard.Services
{
    public class ProducerService
    {
        private readonly HttpClient _http;

        public ProducerService(HttpClient http) => _http = http;

        public async Task<bool> IsRunningAsync()
            => await _http.GetFromJsonAsync<bool>("/producer/is-running");

        public async Task StartAsync()
            => await _http.PostAsync("/producer/start", null);

        public async Task StopAsync()
            => await _http.PostAsync("/producer/stop", null);

        public async Task<int> GetRateAsync()
        {
            var result = await _http.GetFromJsonAsync<RateDto>("/producer/rate");
            return result?.Rate ?? 0;
        }

        public async Task SetRateAsync(int rate)
            => await _http.PostAsync($"/producer/rate/{rate}", null);

        public async Task<double> GetSpeedAsync()
        {
            var result = await _http.GetFromJsonAsync<QueueSpeedDto>("/metrics/speed");
            return result?.QueueSpeed ?? 0;
        }

        public async Task<long> GetQueueLengthAsync()
        {
            var result = await _http.GetFromJsonAsync<QueueLengthDto>("/metrics/length");
            return result?.QueueLength ?? 0;
        }

        public async Task<int> GetConsumerCountAsync()
        {
            // Swagger /producer/consumer/count
            return await _http.GetFromJsonAsync<int>("/producer/consumer/count");
        }
    }
}
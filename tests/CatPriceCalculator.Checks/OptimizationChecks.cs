using System.Globalization;
using System.Net;
using CatPriceCalculator.Core;
public static class OptimizationChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        var inputs = new List<string> { "+1", "-1", " 1 2,3 ", "1\u202f250,50", "1\t2", "1\n", ".1", "1,2.3", "", decimal.MaxValue.ToString(CultureInfo.InvariantCulture), new string('0',4096)+"1,50", new string('9',4096) };
        var random = new Random(82371);
        const string alphabet = "0123456789+-,. \u00a0\u202f\t\nabc";
        for (int i = 0; i < 10000; i++) inputs.Add(new string(Enumerable.Range(0,random.Next(0,200)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray()));
        foreach (var text in inputs)
        {
            var normalized = text.Replace(" ", "").Replace("\u00a0", "").Replace("\u202f", "").Replace(',', '.');
            bool old = decimal.TryParse(normalized,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var oldValue) && oldValue>0;
            bool actual = PriceCalculator.TryPositive(text,out var value);
            if (old != actual || oldValue != value) throw new Exception("Optimized number parsing differs from legacy behavior");
        }
        check(true,"Span parser matches legacy results for 10012 normal, invalid, Unicode and long inputs");
        using (var client = new HttpClient(new SlowBodyHandler()) {Timeout=TimeSpan.FromMilliseconds(20)})
        {
            try {await new ExchangeRateService(client).FetchAsync();throw new Exception("Rate body timeout ignored");}
            catch(HttpRequestException e) {check(e.InnerException is AggregateException a && a.InnerExceptions.Count==2 && a.InnerExceptions.All(x=>x is OperationCanceledException), "ECB and fallback response bodies retain the configured HttpClient deadline");}
        }
        const string xml = "<Envelope xmlns='http://www.ecb.int/vocabulary/2002-08-01/eurofxref'><Cube><Cube time='2026-10-07'><Cube currency='DKK' rate='7.5'/><Cube currency='HUF' rate='400'/></Cube></Cube></Envelope>";
        using (var handler = new DelayedEcbHandler(xml))
        using (var client = new HttpClient(handler))
        {
            var service = new ExchangeRateService(client);
            var quotes = await Task.WhenAll(service.FetchAsync(),service.FetchEuroAsync(),service.FetchEuroHufAsync());
            check(handler.EcbCalls==1 && quotes[0].Rate==400m/7.5m && quotes[1].Rate==1m/7.5m && quotes[2].Rate==400m,"Three overlapping rates share one ECB request with correct cross-rates");
            await service.FetchAsync();
            check(handler.EcbCalls==2,"A later refresh downloads fresh ECB data instead of using completed cache");
        }
        using (var handler = new DelayedEcbHandler(xml,true))
        using (var client = new HttpClient(handler))
        {
            var service = new ExchangeRateService(client);
            var quotes = await Task.WhenAll(service.FetchAsync(),service.FetchEuroAsync(),service.FetchEuroHufAsync());
            check(handler.EcbCalls==1 && handler.FallbackCalls==3 && quotes.All(q=>q.Rate>0),"Shared ECB failure still falls back separately for every currency pair");
        }
        using (var handler = new DelayedEcbHandler(xml))
        using (var client = new HttpClient(handler))
        using (var canceled = new CancellationTokenSource())
        using (var independent = new CancellationTokenSource())
        {
            var service = new ExchangeRateService(client);
            var first = service.FetchAsync(canceled.Token);var second=service.FetchEuroAsync(independent.Token);
            canceled.Cancel();
            try {await first;throw new Exception("Cancellation ignored");}catch(OperationCanceledException) {check(true,"Canceling one scope preserves an independent ECB request");}
            check((await second).Rate==1m/7.5m && handler.EcbCalls==2,"Independent cancellation scopes are never coalesced");
        }
    }
    private sealed class SlowBodyHandler:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new SlowBodyContent()});
    }
    private sealed class SlowBodyContent:HttpContent
    {
        protected override bool TryComputeLength(out long length) {length=0;return false;}
        protected override Task SerializeToStreamAsync(Stream stream,System.Net.TransportContext? context) => throw new Exception("Deadline token must be passed to response body");
        protected override async Task SerializeToStreamAsync(Stream stream,System.Net.TransportContext? context,CancellationToken token) {await Task.Delay(100,token);}
    }
    private sealed class DelayedEcbHandler(string xml,bool fail = false):HttpMessageHandler
    {
        public int EcbCalls,FallbackCalls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(request.RequestUri!.AbsoluteUri==ExchangeRateService.EcbEndpoint)
            {
                Interlocked.Increment(ref EcbCalls);await Task.Delay(30,token);
                return new(fail?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK) {Content=new StringContent(xml)};
            }
            Interlocked.Increment(ref FallbackCalls);
            var euroBase=request.RequestUri.AbsoluteUri==ExchangeRateService.EuroHufEndpoint;
            var euroResult=request.RequestUri.AbsoluteUri==ExchangeRateService.EuroEndpoint;
            return new(HttpStatusCode.OK) {Content=new StringContent($"{{\"amount\":1,\"base\":\"{(euroBase?"EUR":"DKK")}\",\"date\":\"2026-10-07\",\"rates\":{{\"{(euroResult?"EUR":"HUF")}\":{(euroResult?"0.134":"400")}}}}}")};
        }
    }
}

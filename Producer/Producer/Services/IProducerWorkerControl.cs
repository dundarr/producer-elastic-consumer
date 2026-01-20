namespace Producer.Services;

public interface IProducerWorkerControl
{
    void StartProducing();
    void StopProducing();
    bool IsRunning();
    void SetRate(int rate);
    int GetRate();
}
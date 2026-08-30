using TickInputBuffering.Contract;

namespace TickInputBuffering
{
    /// <summary>Single-threaded input boundary. Configure, Seal, then capture and consume.</summary>
    public interface ITickInputBuffer
    {
        bool IsSealed { get; }
        void RegisterButton(int id, bool initiallyDown = false);
        void RegisterAxis(int id, float initialValue = 0f);
        void Seal();
        void CaptureButton(int id, bool isDown);
        void CaptureAxis(int id, float value);
        TickInputFrame ConsumeTick(ulong tick);
    }
}

using Application.Models;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public interface IIconGenerationQueue
{
    ValueTask EnqueueAsync(FileModel file);
    ValueTask<FileModel> DequeueAsync(CancellationToken cancellationToken);
}

public class IconGenerationQueue : IIconGenerationQueue
{
    private readonly Channel<FileModel> _channel;

    public IconGenerationQueue(int capacity = 1000)
    {
        // Можно ограничить capacity, чтобы не накапливать слишком много
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<FileModel>(options);
    }

    public ValueTask EnqueueAsync(FileModel file)
        => _channel.Writer.WriteAsync(file);

    public ValueTask<FileModel> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);
}

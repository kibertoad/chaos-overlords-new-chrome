namespace Rechaos.Core.Assets;

/// <summary>Decodes the packed unsigned 8-bit Smacker audio used by the original movies.</summary>
public static class SmackerAudioDecoder
{
    public static byte[] Decode(SmackerAudioChunk chunk, SmackerAudioTrack track)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(track);
        if (chunk.TrackIndex != track.Index)
            throw new ArgumentException("Smacker audio chunk and track indices do not match.", nameof(chunk));
        if (!track.IsPacked || track.BitsPerSample != 8 || track.Channels is < 1 or > 2)
            throw new NotSupportedException("Only packed mono or stereo unsigned 8-bit Smacker audio is supported.");
        if (chunk.DecodedBytes is not int decodedBytes || decodedBytes < 0
            || decodedBytes > track.MaximumDecodedBytes)
            throw new InvalidDataException("Smacker audio chunk has an invalid decoded length.");
        if (decodedBytes % track.Channels != 0)
            throw new InvalidDataException("Smacker audio chunk does not contain complete samples.");

        var reader = new SmackerBitReader(chunk.EncodedData);
        if (reader.ReadBit() == 0)
            return [];
        var encodedChannels = reader.ReadBit() + 1;
        var isSixteenBit = reader.ReadBit() != 0;
        if (encodedChannels != track.Channels || isSixteenBit)
            throw new InvalidDataException("Smacker audio bitstream format does not match its track metadata.");

        var trees = new SmackerHuffmanTree[encodedChannels];
        for (var channel = 0; channel < encodedChannels; channel++)
            trees[channel] = SmackerHuffmanTree.ReadDelimited(reader);
        if (decodedBytes == 0)
            return [];
        if (decodedBytes < encodedChannels)
            throw new InvalidDataException("Smacker audio chunk is shorter than its channel predictors.");

        var predictors = new byte[encodedChannels];
        for (var channel = encodedChannels - 1; channel >= 0; channel--)
            predictors[channel] = (byte)reader.ReadBits(8);

        var output = new byte[decodedBytes];
        predictors.CopyTo(output, 0);
        for (var index = encodedChannels; index < output.Length; index++)
        {
            var channel = index % encodedChannels;
            predictors[channel] = unchecked((byte)(predictors[channel] + trees[channel].ReadValue(reader)));
            output[index] = predictors[channel];
        }
        return output;
    }
}

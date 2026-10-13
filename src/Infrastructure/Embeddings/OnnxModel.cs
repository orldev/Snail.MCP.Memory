using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Snail.MCP.Memory.Infrastructure.Embeddings;

/// <summary>The loaded sentence transformer: the ONNX session and the SentencePiece tokenizer that feeds it.</summary>
/// <remarks>
/// XLM-RoBERTa quirks handled here: SentencePiece ids carry the fairseq offset, and the session may or may not ask for
/// token type ids. The tokenizer is released with the session rather than kept: its vocabulary is 52 MB of managed
/// heap, and reading it again takes ~0.7 s of the reload (measured on 2026-10-13).
/// </remarks>
internal sealed class OnnxModel : IDisposable
{
    /// <summary>Model input limit is 512; two special tokens are added around the pieces.</summary>
    private const int MaxPieceCount = 510;

    private const long BosId = 0;
    private const long EosId = 2;
    private const long UnknownId = 3;
    private const long FairseqOffset = 1;

    private readonly InferenceSession _session;
    private readonly Tokenizer _tokenizer;
    private readonly int _dimensions;
    private readonly bool _wantsTokenTypes;

    private OnnxModel(InferenceSession session, Tokenizer tokenizer, int dimensions)
    {
        _session = session;
        _tokenizer = tokenizer;
        _dimensions = dimensions;
        _wantsTokenTypes = session.InputMetadata.ContainsKey("token_type_ids");
    }

    /// <summary>Reads the tokenizer and the model from disk into a session answering vectors of <paramref name="dimensions"/>.</summary>
    public static OnnxModel Load(string modelPath, string tokenizerPath, int dimensions)
    {
        Tokenizer tokenizer;

        using (var tokenizerStream = File.OpenRead(tokenizerPath))
            tokenizer = SentencePieceTokenizer.Create(tokenizerStream, addBeginningOfSentence: false, addEndOfSentence: false);

        return new OnnxModel(new InferenceSession(modelPath), tokenizer, dimensions);
    }

    /// <summary>The normalized, mean-pooled embedding of <paramref name="text"/>, prefix included.</summary>
    public float[] Embed(string text)
    {
        var ids = TokenIds(text);
        var length = ids.Length;

        var ones = new long[length];
        Array.Fill(ones, 1L);

        var inputIds = new DenseTensor<long>(ids, [1, length]);
        var attentionMask = new DenseTensor<long>(ones, [1, length]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
        };

        if (_wantsTokenTypes)
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[length], [1, length])));

        using var results = _session.Run(inputs);
        var hidden = results[0].AsTensor<float>();

        return Normalize(MeanPool(hidden, length));
    }

    /// <inheritdoc />
    public void Dispose() => _session.Dispose();

    /// <summary>SentencePiece ids shifted into the fairseq vocabulary, wrapped in BOS/EOS.</summary>
    private long[] TokenIds(string text)
    {
        var pieces = _tokenizer.EncodeToIds(text);
        var count = Math.Min(pieces.Count, MaxPieceCount);

        var ids = new long[count + 2];
        ids[0] = BosId;

        for (var index = 0; index < count; index++)
            ids[index + 1] = pieces[index] == 0 ? UnknownId : pieces[index] + FairseqOffset;

        ids[count + 1] = EosId;

        return ids;
    }

    private float[] MeanPool(Tensor<float> hidden, int length)
    {
        var pooled = new float[_dimensions];

        for (var token = 0; token < length; token++)
            for (var component = 0; component < _dimensions; component++)
                pooled[component] += hidden[0, token, component];

        for (var component = 0; component < _dimensions; component++)
            pooled[component] /= length;

        return pooled;
    }

    private static float[] Normalize(float[] vector)
    {
        var norm = MathF.Sqrt(vector.Sum(component => component * component));

        if (norm > 0)
            for (var component = 0; component < vector.Length; component++)
                vector[component] /= norm;

        return vector;
    }
}

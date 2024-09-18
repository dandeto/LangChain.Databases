using LangChain.Databases;
using LangChain.Providers;
using LangChain.DocumentLoaders;

namespace LangChain.Extensions;

/// <summary>
/// 
/// </summary>
public static class VectorCollectionExtensions
{
    /// <summary>
    /// Return documents most similar to query.
    /// </summary>
    /// <param name="vectorCollection"></param>
    /// <param name="embeddingModel"></param>
    /// <param name="embeddingRequest"></param>
    /// <param name="embeddingSettings"></param>
    /// <param name="searchSettings"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<VectorSearchResponse> SearchAsync(
        this IVectorCollection vectorCollection,
        IEmbeddingModel embeddingModel,
        EmbeddingRequest embeddingRequest,
        EmbeddingSettings? embeddingSettings = default,
        VectorSearchSettings? searchSettings = default,
        CancellationToken cancellationToken = default)
    {
        vectorCollection = vectorCollection ?? throw new ArgumentNullException(nameof(vectorCollection));
        embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));
        searchSettings ??= new VectorSearchSettings();

        if (searchSettings is { Type: VectorSearchType.SimilarityScoreThreshold, ScoreThreshold: null })
        {
            throw new ArgumentException($"ScoreThreshold required for {searchSettings.Type}");
        }

        var response = await embeddingModel.CreateEmbeddingsAsync(
            request: embeddingRequest,
            settings: embeddingSettings,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return await vectorCollection.SearchAsync(new VectorSearchRequest
        {
            Embeddings = [response.ToSingleArray()],
        }, searchSettings, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyCollection<string>> AddDocumentsAsync(
        this IVectorCollection vectorCollection,
        IEmbeddingModel embeddingModel,
        IReadOnlyCollection<Document> documents,
        EmbeddingSettings? embeddingSettings = default,
        CancellationToken cancellationToken = default)
    {
        vectorCollection = vectorCollection ?? throw new ArgumentNullException(nameof(vectorCollection));
        embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));

        return await vectorCollection.AddTextsAsync(
            embeddingModel: embeddingModel,
            texts: documents.Select(x => x.PageContent).ToArray(),
            metadatas: documents.Select(x => x.Metadata).ToArray(),
            embeddingSettings: embeddingSettings,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<Document?> GetDocumentByIdAsync(
        this IVectorCollection vectorCollection,
        string id,
        CancellationToken cancellationToken = default)
    {
        vectorCollection = vectorCollection ?? throw new ArgumentNullException(nameof(vectorCollection));

        var item = await vectorCollection.GetAsync(id, cancellationToken).ConfigureAwait(false);

        return item == null
            ? null
            : new Document(item.Text, item.Metadata?.ToDictionary(x => x.Key, x => x.Value));
    }

    public static async Task<IReadOnlyCollection<string>> AddTextsAsync(
        this IVectorCollection vectorCollection,
        IEmbeddingModel embeddingModel,
        IReadOnlyCollection<string> texts,
        IReadOnlyCollection<IDictionary<string, object>>? metadatas = null,
        EmbeddingSettings? embeddingSettings = default,
        CancellationToken cancellationToken = default)
    {
        vectorCollection = vectorCollection ?? throw new ArgumentNullException(nameof(vectorCollection));
        embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));

        var embeddingRequest = new EmbeddingRequest
        {
            Strings = texts.ToArray(),
            Images = metadatas?
                .Select((metadata, i) => metadata.TryGetValue(texts.ElementAt(i), out object? result)
                    ? result as BinaryData
                    : null)
                .Where(x => x != null)
                .Select(x => Data.FromBytes(x!.ToArray()))
                .ToArray() ?? [],
        };

        float[][] embeddings = await embeddingModel
            .CreateEmbeddingsAsync(embeddingRequest, embeddingSettings, cancellationToken)
            .ConfigureAwait(false);

        return await vectorCollection.AddAsync(
            items: texts.Select((text, i) => new Vector
            {
                Text = text,
                Metadata = metadatas?.ElementAt(i).ToDictionary(x => x.Key, x => x.Value),
                Embedding = embeddings[i],
            }).ToArray(),
            cancellationToken).ConfigureAwait(false);
    }
}
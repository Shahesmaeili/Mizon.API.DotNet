namespace Mizon.API.Client.Operations.Musics;

public sealed class MusicReactionOperation : MizonApiRequest<MusicReactionRequest, MusicReactionResponse>
{
    public MusicReactionOperation(MusicReactionRequest request) : base(HttpMethod.POST, Endpoints.Musics.MusicReaction, request)
    {
        ClientCacheStrategy = ClientCacheStrategy.Disabled;
    }
}
namespace ClaudeCliProtocol
{
    /// <summary>
    /// An image sent with a message: its media type (<c>image/png</c>) and its encoded bytes in base64. Only data: the
    /// library neither decodes nor measures it.
    /// </summary>
    public sealed record ImageData(string MediaType, string Data);
}

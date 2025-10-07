namespace LocalLlmAssistant.Models;

public class RagConfig
{
    public int ChunkSize { get; set; } = 800;
    public int FtsCandidates { get; set; } = 200;
    public int TopK { get; set; } = 4;
    public double MmrLambda { get; set; } = 0.5;
}

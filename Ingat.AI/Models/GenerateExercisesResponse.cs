namespace Ingat.AI.Models;

public sealed class GenerateExercisesResponse
{
    public List<GeneratedExercise> Exercises { get; set; } = new();
}

public sealed class GeneratedExercise
{
    public string Question { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public int CorrectIndex { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

namespace DataShare.Api.Services;

public interface IFileTypeValidationService
{
    /// <summary>
    /// Detecte le type reel du fichier via sa signature binaire (magic bytes) et le valide
    /// contre l'allowlist et la coherence avec l'extension declaree.
    /// </summary>
    /// <returns>Le content-type reellement detecte si valide, sinon null.</returns>
    Task<string?> DetectAndValidateAsync(Stream content, string declaredFileName);
}

using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Definitions.Licensing;

namespace DataShare.Api.Services;

public class FileTypeValidationService : IFileTypeValidationService
{
    // Cout de construction eleve (charge tout le catalogue de signatures) - une seule instance
    // partagee pour toute la duree de vie de l'application, jamais reconstruite par requete.
    private static readonly IContentInspector Inspector = new ContentInspectorBuilder
    {
        Definitions = new ExhaustiveBuilder { UsageType = UsageType.PersonalNonCommercial }.Build()
    }.Build();

    // Allowlist volontairement restreinte pour ce MVP - a documenter/etendre dans SECURITY.md (Etape 5).
    private static readonly Dictionary<string, string[]> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = new[] { ".pdf" },
        ["image/png"] = new[] { ".png" },
        ["image/jpeg"] = new[] { ".jpg", ".jpeg" },
        ["image/gif"] = new[] { ".gif" },
        ["application/zip"] = new[] { ".zip" }
    };

    private readonly ILogger<FileTypeValidationService> _logger;

    public FileTypeValidationService(ILogger<FileTypeValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Lit la signature binaire (magic bytes) du contenu pour determiner son type MIME reel,
    /// independamment de ce que le client pretend (nom de fichier, Content-Type declare).
    /// </summary>
    /// <param name="content">
    /// Le flux binaire du fichier uploade (ses premiers octets contiennent la signature a lire).
    /// </param>
    /// <param name="declaredFileName">
    /// Le nom de fichier tel qu'envoye par le client (ex. "photo.pdf"). Sert uniquement a en
    /// extraire l'extension declaree, pour verifier qu'elle correspond bien au type reellement
    /// detecte dans le contenu - pas a determiner le type lui-meme (l'extension seule n'est pas
    /// fiable, cf. docs/adr/0002-validation-type-fichiers.md).
    /// </param>
    /// <returns>
    /// Le content-type reellement detecte (ex. "application/pdf") si son type est dans
    /// l'allowlist ET coherent avec l'extension declaree ; sinon null, ce qui signifie pour
    /// l'appelant "rejette cet upload" (signature illisible, type non autorise, ou incoherence
    /// entre le contenu reel et l'extension annoncee - ex. un .exe renomme en .pdf).
    /// </returns>
    public Task<string?> DetectAndValidateAsync(Stream content, string declaredFileName)
    {
        var initialPosition = content.CanSeek ? content.Position : 0;

        var results = Inspector.Inspect(content);

        // Le flux doit rester utilisable ensuite pour l'ecriture reelle du fichier sur disque.
        if (content.CanSeek)
        {
            content.Position = initialPosition;
        }

        var match = results.ByMimeType().FirstOrDefault();
        if (match is null)
        {
            _logger.LogWarning("File type detection: no signature match for {DeclaredFileName}", declaredFileName);
            return Task.FromResult<string?>(null);
        }

        if (!AllowedTypes.TryGetValue(match.MimeType, out var expectedExtensions))
        {
            _logger.LogWarning(
                "File type detection: {MimeType} detected for {DeclaredFileName} is not in the allowlist",
                match.MimeType, declaredFileName);
            return Task.FromResult<string?>(null);
        }

        var declaredExtension = Path.GetExtension(declaredFileName);
        var isCoherentWithDeclaredExtension = expectedExtensions
            .Contains(declaredExtension, StringComparer.OrdinalIgnoreCase);

        if (!isCoherentWithDeclaredExtension)
        {
            _logger.LogWarning(
                "File type detection: declared extension {DeclaredExtension} does not match detected type {MimeType} for {DeclaredFileName}",
                declaredExtension, match.MimeType, declaredFileName);
            return Task.FromResult<string?>(null);
        }

        _logger.LogDebug("File type detection: {MimeType} confirmed for {DeclaredFileName}", match.MimeType, declaredFileName);
        return Task.FromResult<string?>(match.MimeType);
    }
}

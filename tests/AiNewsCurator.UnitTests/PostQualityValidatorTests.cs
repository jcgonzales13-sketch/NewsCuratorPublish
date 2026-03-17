using AiNewsCurator.Application.Services;

namespace AiNewsCurator.UnitTests;

public sealed class PostQualityValidatorTests
{
    [Fact]
    public void Should_Reject_Post_With_Forbidden_Phrase()
    {
        var text = "Essa IA vai mudar tudo para sempre e todo mundo precisa usar isso agora.";

        var result = PostQualityValidator.Validate(text);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Should_Accept_Well_Formed_Post()
    {
        var text = "Uma noticia recente sobre IA merece atencao. O anuncio mostra como inteligencia artificial esta entrando em fluxos reais de produto e operacao. Para empresas, o impacto esta menos no hype e mais na capacidade de avaliar casos de uso, riscos e retorno com criterio.";

        var result = PostQualityValidator.Validate(text);

        Assert.True(result.IsValid);
    }
}

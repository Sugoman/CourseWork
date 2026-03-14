using LearningAPI.Services;
using LearningTrainerShared.Models.KnowledgeTreeDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningAPI.Controllers;

[ApiController]
[Route("api/knowledge-tree")]
[Authorize]
public class KnowledgeTreeController : BaseApiController
{
    private readonly IKnowledgeTreeService _treeService;
    private readonly ILogger<KnowledgeTreeController> _logger;

    public KnowledgeTreeController(
        IKnowledgeTreeService treeService,
        ILogger<KnowledgeTreeController> logger)
    {
        _treeService = treeService;
        _logger = logger;
    }

    /// <summary>
    /// Получить полное состояние Дерева Знаний (с вложенной иерархией Branch→Twig→Leaf)
    /// </summary>
    [HttpGet("state")]
    [ProducesResponseType(typeof(KnowledgeTreeState), 200)]
    public async Task<IActionResult> GetTreeState(CancellationToken ct = default)
    {
        var userId = GetUserId();
        var state = await _treeService.GetTreeStateAsync(userId, ct);
        return Ok(state);
    }

    /// <summary>
    /// Влить XP пользователя в Дерево Знаний
    /// </summary>
    [HttpPost("pour-xp")]
    [ProducesResponseType(typeof(PourXpResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> PourXp([FromBody] PourXpRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            return BadRequest("Количество XP должно быть положительным");

        var userId = GetUserId();
        var result = await _treeService.PourXpAsync(userId, request.Amount, ct);

        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(result);
    }

    /// <summary>
    /// Получить список доступных скинов дерева
    /// </summary>
    [HttpGet("skins")]
    [ProducesResponseType(typeof(List<TreeSkinInfo>), 200)]
    public async Task<IActionResult> GetSkins(CancellationToken ct = default)
    {
        var userId = GetUserId();
        var skins = await _treeService.GetAvailableSkinsAsync(userId, ct);
        return Ok(skins);
    }

    /// <summary>
    /// Сменить скин дерева
    /// </summary>
    [HttpPut("skin")]
    public async Task<IActionResult> ChangeSkin([FromBody] ChangeSkinRequest request, CancellationToken ct = default)
    {
        var userId = GetUserId();
        var result = await _treeService.ChangeSkinAsync(userId, request.SkinId, ct);
        if (!result)
            return BadRequest("Скин не найден");

        return Ok();
    }
}

public class ChangeSkinRequest
{
    public int SkinId { get; set; }
}

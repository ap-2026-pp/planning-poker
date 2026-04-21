using Microsoft.AspNetCore.Mvc;
using PlanningPoker.Domain.DTOs;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.Api.Controllers;

// [ApiController]
// [Route("api/[controller]")]
// public class SamplesController : ControllerBase
// {
//     private readonly ISampleService _service;

//     public SamplesController(ISampleService service) => _service = service;

//     [HttpGet]
//     public async Task<IReadOnlyList<SampleDto>> List(CancellationToken ct) =>
//         await _service.ListAsync(ct);

//     [HttpGet("{id:int}")]
//     public async Task<ActionResult<SampleDto>> Get(int id, CancellationToken ct)
//     {
//         var result = await _service.GetAsync(id, ct);
//         return result is null ? NotFound() : Ok(result);
//     }

//     [HttpPost]
//     public async Task<ActionResult<SampleDto>> Create([FromBody] CreateSampleDto dto, CancellationToken ct)
//     {
//         var created = await _service.CreateAsync(dto, ct);
//         return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
//     }
// }
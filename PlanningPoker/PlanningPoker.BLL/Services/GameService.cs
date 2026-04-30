using System.Security.Cryptography;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

public class GameService(
    IGameRepository gameRepository,
    ICurrentUserContext currentUserContext) : IGameService
{
    private const int InviteCodeLength = 20;
    private const string InviteCodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    
    /// <summary>
    /// Створює нову ігрову сесію та автоматично додає поточного користувача
    /// як першого учасника з роллю Master.
    /// </summary>
    /// <param name="createGameRequestDto">Дані для створення гри.</param>
    /// <returns>Створену гру у вигляді <see cref="GameDto"/></returns>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо в поточного користувача вже існує гра з такою ж назвою.
    /// </exception>
    public async Task<GameDto> AddGameAsync(CreateGameRequestDto createGameRequestDto)
    {
        var currentUser = await currentUserContext.GetRequiredUserAsync();
        var game = GameMapper.ToGame(createGameRequestDto);
        await EnsureUniqueGameNameAsync(game.Name, currentUser.Id);

        var hostDisplayName = string.IsNullOrWhiteSpace(createGameRequestDto.HostDisplayName)
            ? currentUser.DisplayName
            : createGameRequestDto.HostDisplayName.Trim();
        
        game.CreatedBy = currentUser.Id;
        game.InviteCode = await GenerateInviteCodeAsync();
        game.Participants =
        [
            new GameParticipant
            {
                Id = Guid.NewGuid(),
                UserId = currentUser.Id,
                DisplayName = hostDisplayName,
                Role = ParticipantRole.Master,
                JoinedAt = DateTime.UtcNow,
                IsConnected = true
            }
        ];
        
        await gameRepository.AddAsync(game);
        await gameRepository.SaveChangesAsync();
        
        return GameMapper.ToGameDto(game);
    }

    /// <summary>
    /// Повертає гру за її ідентифікатором.
    /// </summary>
    /// <param name="gameId">Унікальний ідентифікатор гри.</param>
    /// <returns>Гру у вигляді <see cref="GameDto"/>.</returns>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру з вказаним ідентифікатором не знайдено.
    /// </exception>
    public async Task<GameDto> GetGameByIdAsync(Guid gameId)
    {
        var game = await GetGameOrThrowAsync(gameId);
        return GameMapper.ToGameDto(game);
    }

    /// <summary>
    /// Оновлює параметри існуючої гри.
    /// Операція доступна лише користувачу з роллю Master у цій грі.
    /// </summary>
    /// <param name="gameId">Унікальний ідентифікатор гри.</param>
    /// <param name="updateGameRequestDto">DTO з новими параметрами гри.</param>
    /// <returns>Оновлену гру у вигляді <see cref="GameDto"/>.</returns>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру не знайдено.
    /// </exception>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо поточний користувач не є Master цієї гри.
    /// </exception>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо нова назва гри вже використовується поточним власником в іншій грі.
    /// </exception>
    public async Task<GameDto> UpdateGameAsync(Guid gameId, UpdateGameRequestDto updateGameRequestDto)
    {
        var userId = currentUserContext.GetRequiredUserId();
        var existingGame = await GetGameOrThrowAsync(gameId);
        EnsureUserIsGameMaster(existingGame, userId, "update");

        var updatedGame = GameMapper.ToGame(updateGameRequestDto);
        await EnsureUniqueGameNameAsync(updatedGame.Name, existingGame.CreatedBy, gameId);

        existingGame.Name = updatedGame.Name;
        existingGame.AutoRevealCards = updatedGame.AutoRevealCards;
        existingGame.IsActive = updatedGame.IsActive;
        existingGame.ShowAverage = updatedGame.ShowAverage;
        existingGame.VotingSystem = updatedGame.VotingSystem;
        existingGame.ShowCountdownAnimation = updatedGame.ShowCountdownAnimation;
        
        gameRepository.Update(existingGame);
        await gameRepository.SaveChangesAsync();
        
        return GameMapper.ToGameDto(existingGame);
    }

    /// <summary>
    /// Виконує soft delete гри, позначаючи її неактивною та видаленою.
    /// Операція доступна лише Master учаснику.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Асинхронна операція без повернення значення.</returns>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо поточний користувач не є Master цієї гри.
    /// </exception>
    public async Task DeleteGameAsync(Guid gameId)
    {
        var userId = currentUserContext.GetRequiredUserId();
        var game = await gameRepository.GetByIdAsync(gameId);
        
        if (game is null)
        {
            return;
        }
        
        EnsureUserIsGameMaster(game, userId, "delete");
        game.IsActive = false;
        game.IsDeleted = true;
        gameRepository.Update(game);
        await gameRepository.SaveChangesAsync();
    }
    
    /// <summary>
    /// Повертає гру для формування інвайт-посилання, якщо поточний користувач є її учасником.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Сутність гри з даними для запрошення.</returns>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру не знайдено.
    /// </exception>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо поточний користувач не є учасником цієї гри.
    /// </exception>
    public async Task<Game> GetGameInviteAsync(Guid gameId)
    {
        var userId = currentUserContext.GetRequiredUserId();
        var game = await GetGameOrThrowAsync(gameId);
        EnsureUserIsParticipant(game, userId, "view", "game invite");

        return game;
    }
    
    /// <summary>
    /// Генерує унікальний інвайт-код для гри.
    /// Код формується випадково на основі дозволеного набору символів
    /// і перевіряється на унікальність.
    /// </summary>
    /// <returns>Унікальний інвайт-код.</returns>
    private async Task<string> GenerateInviteCodeAsync()
    {
        var inviteCodeBuffer = new char[InviteCodeLength];
        string inviteCode;

        do
        {
            for (var i = 0; i < inviteCodeBuffer.Length; i++)
            {
                inviteCodeBuffer[i] = InviteCodeAlphabet[RandomNumberGenerator.GetInt32(InviteCodeAlphabet.Length)];
            }

            inviteCode = new string(inviteCodeBuffer);
        } while (await gameRepository.ExistsByInviteCodeAsync(inviteCode));

        return inviteCode;
    }

    /// <summary>
    /// Повертає гру за ідентифікатором або викидає виняток, якщо гру не знайдено.
    /// </summary>
    /// <param name="gameId">Ідентифікатор гри.</param>
    /// <returns>Сутність гри.</returns>
    /// <exception cref="NotFoundException">
    /// Виникає, якщо гру не знайдено.
    /// </exception>
    private async Task<Game> GetGameOrThrowAsync(Guid gameId)
    {
        return await gameRepository.GetByIdAsync(gameId)
               ?? throw new NotFoundException(nameof(Game), gameId);
    }

    /// <summary>
    /// Перевіряє, чи не існує в користувача іншої гри з такою ж назвою.
    /// Під час оновлення поточну гру можна виключити з перевірки.
    /// </summary>
    /// <param name="gameName">Назва гри для перевірки.</param>
    /// <param name="createdBy">Ідентифікатор користувача-власника гри.</param>
    /// <param name="excludedGameId">
    /// Ідентифікатор гри, яку потрібно виключити з перевірки
    /// (використовується при оновленні).
    /// </param>
    /// <returns>Асинхронна операція без повернення значення.</returns>
    /// <exception cref="ResourceAlreadyExistsException">
    /// Виникає, якщо назва вже використовується.
    /// </exception>
    private async Task EnsureUniqueGameNameAsync(string gameName, Guid createdBy, Guid? excludedGameId = null)
    {
        var exists = excludedGameId.HasValue
            ? await gameRepository.ExistsByNameAsync(gameName, createdBy, excludedGameId.Value)
            : await gameRepository.ExistsByNameAsync(gameName, createdBy);

        if (exists)
        {
            throw new ResourceAlreadyExistsException(nameof(Game), gameName);
        }
    }

    /// <summary>
    /// Перевіряє, чи є поточний користувач Master конкретної гри.
    /// </summary>
    /// <param name="game">Гра, для якої виконується перевірка.</param>
    /// <param name="currentUserId">Ідентифікатор поточного користувача.</param>
    /// <param name="action">Назва дії, яку користувач намагається виконати.</param>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо користувач не є Master цієї гри.
    /// </exception>
    private static void EnsureUserIsGameMaster(Game game, Guid currentUserId, string action)
    {
        var isGameMaster = game.Participants.Any(participant =>
            participant.UserId == currentUserId &&
            participant.Role == ParticipantRole.Master);

        if (!isGameMaster)
        {
            throw new ForbiddenException(action, "game");
        }
    }

    /// <summary>
    /// Перевіряє, чи є поточний користувач учасником гри.
    /// </summary>
    /// <param name="game">Гра, для якої виконується перевірка.</param>
    /// <param name="currentUserId">Ідентифікатор поточного користувача.</param>
    /// <param name="action">Назва дії, яку користувач намагається виконати.</param>
    /// <param name="resourceName">Назва ресурсу, до якого виконується доступ.</param>
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо користувач не є учасником гри.
    /// </exception>
    private static void EnsureUserIsParticipant(Game game, Guid currentUserId, string action, string resourceName)
    {
        var isParticipant = game.Participants.Any(participant => participant.UserId == currentUserId);
        if (!isParticipant)
        {
            throw new ForbiddenException(action, resourceName);
        }
    }
}

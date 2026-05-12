using System.ComponentModel.DataAnnotations;
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
    ICurrentUserContext currentUserContext,
    IGameAccessService gameAccessService) : IGameService
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
        if (createGameRequestDto.VotingSystem == VotingSystem.Custom)
        {
            ValidateCustomValues(createGameRequestDto.CustomValues);

            createGameRequestDto.CustomValues = string.Join(", ", createGameRequestDto.CustomValues
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

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
    /// <exception cref="ForbiddenException">
    /// Виникає, якщо поточний користувач не є учасником гри.
    /// </exception>
    public async Task<GameDto> GetGameByIdAsync(Guid gameId)
    {
        await gameAccessService.GetRequiredParticipantAsync(gameId);
        var game = await GetGameOrThrowAsync(gameId);
        await gameAccessService.GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.ViewAction,
            AccessControlConstants.GameResource);
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
        var existingGame = await GetGameOrThrowAsync(gameId);
        await gameAccessService.GetRequiredMasterAsync(
            gameId,
            AccessControlConstants.UpdateAction,
            AccessControlConstants.GameResource);

        var updatedGame = GameMapper.ToGame(updateGameRequestDto);
        await EnsureUniqueGameNameAsync(updatedGame.Name, existingGame.CreatedBy, gameId);

        existingGame.Name = updatedGame.Name;
        existingGame.AutoRevealCards = updatedGame.AutoRevealCards;
        existingGame.IsActive = updatedGame.IsActive;
        existingGame.RevealPolicy = updatedGame.RevealPolicy;
        existingGame.IssuesPolicy = updatedGame.IssuesPolicy;
        existingGame.ShowAverage = updatedGame.ShowAverage;
        existingGame.VotingSystem = updatedGame.VotingSystem;
        existingGame.ShowCountdownAnimation = updatedGame.ShowCountdownAnimation;
        existingGame.EnableFunFeatures = updatedGame.EnableFunFeatures;
        
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
        var game = await gameRepository.GetByIdAsync(gameId);
        
        if (game is null)
        {
            return;
        }

        await gameAccessService.GetRequiredMasterAsync(
            gameId,
            AccessControlConstants.DeleteAction,
            AccessControlConstants.GameResource);
        
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
        await gameAccessService.GetRequiredParticipantAsync(gameId);
        return await GetGameOrThrowAsync(gameId);
    }
    
     /// <summary>
    /// Повертає список ігор поточного користувача відповідно до вибраного типу вибірки.
    /// Може повертати лише створені ігри, лише ігри, у яких користувач брав участь (крім створених),
    /// або всі разом.
    /// </summary>
    /// <param name="scope">
    ///     Тип вибірки ігор:
    ///     Created — тільки створені користувачем,
    ///     Participated — тільки ті, у яких він був учасником,
    ///     All — усі доступні для цього користувача ігри.
    /// </param>
    /// <returns>Колекцію ігор користувача у вигляді <see cref="UserGameDto"/>.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Виникає, якщо не вдалося визначити поточного авторизованого користувача.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Виникає, якщо передано непідтримуване значення <paramref name="scope"/>.
    /// </exception>
    public async Task<IEnumerable<UserGameDto>?> GetUserGamesAsync(UserGamesScope scope)
    {
        var currentUserId = currentUserContext.GetRequiredUserId();
        return scope switch
        {
            UserGamesScope.Created => await GetUserCreatedGames(currentUserId),
            UserGamesScope.Participated => await GetUserParticipatedGames(currentUserId),
            UserGamesScope.All => await GetAllUserGames(currentUserId),
            _ => throw new InvalidOperationException()
        };
    }
    
    /// <summary>
    /// Повертає повний список ігор, пов’язаних з поточним користувачем:
    /// як створених ним, так і тих, у яких він брав участь.
    /// </summary>
    /// <param name="currentUserId">Ідентифікатор поточного користувача.</param>
    /// <returns>Колекцію ігор користувача у вигляді <see cref="UserGameDto"/>.</returns>
    private async Task<IEnumerable<UserGameDto>?> GetAllUserGames(Guid currentUserId)
    {
        var games = await gameRepository.GetAllByUserId(currentUserId);
        return (games ?? []).Select(game => GameMapper.ToUserGameDto(game, currentUserId));
    }
    
    /// <summary>
    /// Повертає список ігор, у яких поточний користувач брав участь,
    /// але не обов’язково був їхнім творцем.
    /// </summary>
    /// <param name="currentUserId">Ідентифікатор поточного користувача.</param>
    /// <returns>Колекцію ігор користувача у вигляді <see cref="UserGameDto"/>.</returns>
    private async Task<IEnumerable<UserGameDto>?> GetUserParticipatedGames(Guid currentUserId)
    {
        var games = await gameRepository.GetByUserIdParticipated(currentUserId);
        return (games ?? []).Select(game => GameMapper.ToUserGameDto(game, currentUserId));
    }
    
    /// <summary>
    /// Повертає список ігор, створених поточним користувачем.
    /// </summary>
    /// <param name="currentUserId">Ідентифікатор поточного користувача.</param>
    /// <returns>Колекцію створених ігор у вигляді <see cref="UserGameDto"/>.</returns>
    private async Task<IEnumerable<UserGameDto>?> GetUserCreatedGames(Guid currentUserId)
    {
        var games = await gameRepository.GetCreatedByUserId(currentUserId);
        return (games ?? []).Select(game => GameMapper.ToUserGameDto(game, currentUserId));
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

    private void ValidateCustomValues(string? customValues)
    {
        if (string.IsNullOrWhiteSpace(customValues))
            throw new ValidationException("Custom values can't be empty");

        var cards = customValues.Split(',')
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();

        if (cards.Count < 2)
            throw new ValidationException("You need at least 2 cards");

        if (cards.Count > 13)
            throw new ValidationException("Maximum 15 cards allowed, you can write 13 and also break and question cards.");

        if (cards.Distinct(StringComparer.OrdinalIgnoreCase).Count() != cards.Count)
            throw new ValidationException("Dublicate card values aren't allowed!");

        var regex = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z0-9.\-\s]+$");
        foreach (var card in cards)
        {
            if (card.Length > 3)
                throw new ValidationException($"Card '{card}' is too long. Max 3 characters!");

            if (!regex.IsMatch(card))
                throw new ValidationException($"Card '{card}' contains forbidden characters. Use only letters, numbers, dots or dashes.");
        }
    }
}
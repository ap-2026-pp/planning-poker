using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using PlanningPoker.BLL.Constants;
using PlanningPoker.Domain.DTOs.Game;
using PlanningPoker.Domain.Exceptions;
using PlanningPoker.Domain.Interfaces.Repositories;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Mappers;
using PlanningPoker.Domain.Models;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Керує життєвим циклом ігрових сесій:
/// створенням, переглядом, оновленням, видаленням та запрошеннями.
/// </summary>
public class GameService(
    IGameRepository gameRepository,
    ICurrentUserContext currentUserContext,
    IGameAccessService gameAccessService,
    IGameRealtimeService gameRealtimeService) : IGameService
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

        var hostDisplayName = string.IsNullOrWhiteSpace(createGameRequestDto.DisplayName)
            ? currentUser.DisplayName
            : createGameRequestDto.DisplayName.Trim();

        game.CreatedBy = currentUser.Id;
        game.AutoResetTimer = createGameRequestDto.AutoResetTimer;
        game.AutoRevealCards = createGameRequestDto.AutoRevealCards;
        game.IssuesPolicy = createGameRequestDto.IssuesPolicy;
        game.RevealPolicy = createGameRequestDto.RevealPolicy;
        game.EnableFunFeatures = createGameRequestDto.EnableFunFeatures;
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
        var game = await GetGameOrThrowAsync(gameId);

        await gameAccessService.GetParticipantOrEnsureOwnerAsync(
            game,
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
    public async Task<GameDto> UpdateGameAsync(Guid gameId, UpdateGameRequestDto dto)
    {
        var game = await GetGameOrThrowAsync(gameId);

        await gameAccessService.EnsureCanUpdateGameAsync(game);

        if (dto.VotingSystem == VotingSystem.Custom)
        {
            ValidateCustomValues(dto.CustomValues);

            dto.CustomValues = string.Join(", ",
                dto.CustomValues!
                    .Split(',')
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase));
        }
        else
        {
            dto.CustomValues = null;
        }

        await EnsureUniqueGameNameAsync(dto.Name, game.CreatedBy, gameId);

        game.Name = dto.Name.Trim();
        game.VotingSystem = dto.VotingSystem;
        game.CustomValues = dto.CustomValues;
        game.RevealPolicy = dto.RevealPolicy;
        game.IssuesPolicy = dto.IssuesPolicy;
        game.AutoRevealCards = dto.AutoRevealCards;
        game.ShowAverage = dto.ShowAverage;
        game.DefaultTimerMinutes = dto.DefaultTimerMinutes; // Час таймера
        game.AutoResetTimer = dto.AutoResetTimer;
        game.ShowCountdownAnimation = dto.ShowCountdownAnimation;
        game.IsActive = dto.IsActive;
        game.EnableFunFeatures = dto.EnableFunFeatures;
        
        var revealAllowedIds = dto.RevealAllowedParticipantIds.ToHashSet();
        var issuesAllowedIds = dto.IssuesAllowedParticipantIds.ToHashSet();

        foreach (var participant in game.Participants.Where(x => x.RemovedAt == null))
        {
            participant.CanRevealCards =
                dto.RevealPolicy == RevealPolicy.SpecificParticipants &&
                revealAllowedIds.Contains(participant.Id);

            participant.CanManageIssues =
                dto.IssuesPolicy == IssuesPolicy.SpecificParticipants &&
                issuesAllowedIds.Contains(participant.Id);
        }

        gameRepository.Update(game);
        await gameRepository.SaveChangesAsync();

        await gameRealtimeService.NotifyGameUpdatedAsync(game);

        foreach (var participant in game.Participants.Where(x => x.RemovedAt == null))
        {
            await gameRealtimeService.NotifyParticipantUpdatedAsync(
                game,
                ParticipantMapper.ToGameParticipantDto(participant));
        }

        return GameMapper.ToGameDto(game);
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

        await gameAccessService.EnsureCanDeleteGameAsync(game);
        
        game.IsActive = false;
        game.IsDeleted = true;
        gameRepository.Update(game);
        await gameRepository.SaveChangesAsync();
        await gameRealtimeService.NotifyGameUpdatedAsync(game);
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
        await gameAccessService.GetRequiredParticipantAsync(
            gameId,
            AccessControlConstants.ViewAction,
            AccessControlConstants.GameResource);
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

    /// <summary>
    /// Перевіряє коректність значень карт, введених користувачем.
    /// </summary>
    /// <param name="customValues">Рядок із значеннями карт, розділеними комами.</param>
    /// <exception cref="ValidationException">Виникає, якщо значення карт некоректні.</exception>
    private void ValidateCustomValues(string? customValues)
    {
        if (string.IsNullOrWhiteSpace(customValues))
            throw new ValidationException("Значення карт не можуть бути порожніми");

        var cards = customValues.Split(',')
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();

        if (cards.Count < 2)
            throw new ValidationException("Потрібно щонайменше 2 карти");

        if (cards.Count > 13)
            throw new ValidationException("Максимум дозволено 13 карт (можна також додати break та question cards)");

        if (cards.Distinct(StringComparer.OrdinalIgnoreCase).Count() != cards.Count)
            throw new ValidationException("Значення карт не можуть повторюватися");

        var regex = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z0-9.\-\s]+$");

        foreach (var card in cards)
        {
            if (card.Length > 3)
                throw new ValidationException($"Карта '{card}' занадто довга. Максимум 3 символи.");

            if (!regex.IsMatch(card))
                throw new ValidationException(
                    $"Карта '{card}' містить заборонені символи. Дозволені лише літери, цифри, крапки та дефіси.");
        }
    }
}

using GtaJournal.Data.Repositories;
using GtaJournal.Models;

namespace GtaJournal.Services;

/// <summary>Опциональная система тегов подразделений.</summary>
public sealed class TagService
{
    private readonly UnitTagRepository _repository;
    private readonly SettingsService _settings;

    public TagService(UnitTagRepository repository, SettingsService settings)
    {
        _repository = repository;
        _settings = settings;
        Tags = repository.GetAll();
    }

    public bool Enabled => _settings.Current.UseTags;

    public IReadOnlyList<UnitTag> Tags { get; private set; }

    public event Action? Changed;

    /// <summary>Тег подразделения для тега из ника; null, если система выключена или совпадений нет.</summary>
    public UnitTag? Match(string? nickTag) =>
        !Enabled || string.IsNullOrWhiteSpace(nickTag)
            ? null
            : Tags.FirstOrDefault(t => nickTag.Contains(t.Tag, StringComparison.OrdinalIgnoreCase));

    public async Task SaveAsync(IReadOnlyList<UnitTag> tags)
    {
        await _repository.ReplaceAllAsync(tags);
        Tags = _repository.GetAll();
        Changed?.Invoke();
    }
}

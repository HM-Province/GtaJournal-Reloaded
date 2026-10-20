# GTA Journal (WinUI 3)

Клиент для status-journal.com: статусы, списки онлайна, статистика, управление пользователями, несколько аккаунтов, 2FA.

## Структура

```
GtaJournal/
  Core/Navigation/   роутер: RouteRegistry, NavigationService (гарды), RoutedPage, AppRoutes (все страницы)
  Data/              SQLite: Database, репозитории, Migrations/ (раннер + Scripts/*.sql)
  Models/            модели журнала и аккаунта
  Services/          аккаунты, статус и наблюдение за процессом, HTTP-клиент журнала и парсер, звук, уведомления, трей
  ViewModels/        MVVM (CommunityToolkit.Mvvm), общие зависимости — ViewModelContext
  Views/             страницы (XAML, x:Bind)
  Controls/          переиспользуемые контролы (календарь онлайна)
  Styles/            общие стили
```

## Как добавить страницу

1. `Views/FooPage.xaml` — корневой элемент `nav:RoutedPage`; в code-behind:
   `ViewModel = App.GetService<FooViewModel>()` и `protected override object? NavigationTarget => ViewModel;`
2. `ViewModels/FooViewModel.cs` — наследник `ViewModelBase`; параметр перехода приходит в `OnNavigatedToAsync`.
3. Зарегистрировать VM в `App.ConfigureServices`: `services.AddTransient<FooViewModel>();`
4. Добавить маршрут в `Core/Navigation/AppRoutes.cs`:
   ```csharp
   .Add<FooPage>("foo", r => r with { Title = "Foo", Glyph = "", Menu = MenuPlacement.Main, Order = 40 })
   ```
   Пункт меню появится сам. Флаги `RequiresSession` / `RequiresAdmin` проверяются гардами.

Переход: `Navigation.Navigate("foo", параметр)`.

## Как добавить миграцию

Положить файл `Data/Migrations/Scripts/NNNN_Name.sql` с номером больше последнего. Скрипты вшиваются в сборку
(EmbeddedResource), применяются при запуске по порядку имён, каждый в своей транзакции; применённые
записываются в `__migrations_history`. Регистрировать ничего не нужно.

## Публикация

```
dotnet publish -c Release -p:Platform=x64 -p:PublishProfile=win-x64
```

Тримминг выключен намеренно: CsWinRT не генерирует WinRT-vtable для коллекций, передаваемых в `ItemsSource`,
и тримленная сборка падает при запуске.

Данные приложения: `%LOCALAPPDATA%\GtaJournal\journal.db`, ошибки — `error.log` там же.

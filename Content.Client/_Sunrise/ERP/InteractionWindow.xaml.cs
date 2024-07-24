// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/space-sunrise/lust-station/blob/master/CLA.txt
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.AutoGenerated;
using Content.Shared._Sunrise.ERP;
using Robust.Client.GameObjects;
using Content.Shared.Humanoid;
using Robust.Client.UserInterface.Controls;
using Content.Shared.IdentityManagement;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Client.Graphics;
using Robust.Shared.Timing;
using System.Linq;
using Content.Shared.Hands.Components;
using Robust.Shared.Utility;
using Content.Client.Stylesheets;
using Content.Shared._Sunrise.ERP.Components;
using System.IO;
using Content.Shared.Interaction;
namespace Content.Client._Sunrise.ERP;

[GenerateTypedNameReferences]
public sealed partial class InteractionWindow : DefaultWindow
{
    private readonly SpriteSystem _spriteSystem;
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    public NetEntity? TargetEntityId { get; set; }
    public Sex? UserSex { get; set; }
    public Sex? TargetSex { get; set; }
    public bool UserHasClothing { get; set; }
    public bool TargetHasClothing { get; set; }
    public bool Erp { get; set; }
    public ProgressBar LoveBar;
    public TimeSpan TimeUntilAllow = TimeSpan.Zero;
    private readonly InteractionEui _eui;
    public TimeSpan UntilUpdate = TimeSpan.Zero;
    public InteractionWindow(InteractionEui eui)
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);
        _spriteSystem = _entManager.System<SpriteSystem>();
        _eui = eui;
        LoveBar = ProgressBar;
        SearchBar.OnTextChanged += SearchBarOnOnTextChanged;
        ProgressBar.ForegroundStyleBoxOverride = new StyleBoxFlat(backgroundColor: Color.Pink);
        ButtonGroup Group = new();
        InteractionButton.Group = Group;
        DescriptionButton.Group = Group;
        InteractionButton.Pressed = true;
        InteractionButton.OnPressed += SetModeToInteraction;
        DescriptionButton.OnPressed += SetModeToDescription;
        PopulateByFilter("", false);
    }

    private void SetModeToInteraction(BaseButton.ButtonEventArgs obj)
    {
        SearchBar.Visible = true;
        ItemInteractions.Visible = true;
        DescriptionContainer.Visible = false;
    }

    private void SetModeToDescription(BaseButton.ButtonEventArgs obj)
    {
        SearchBar.Visible = false;
        ItemInteractions.Visible = false;
        DescriptionContainer.Visible = true;
        DescriptionPopulate();
    }

    private void DescriptionPopulate()
    {
        TextureLeft.DisposeAllChildren();
        TextureRight.DisposeAllChildren();

        if (!_player.LocalEntity.HasValue) return;
        if (!UserSex.HasValue) return;
        if (!_entManager.TryGetComponent<InteractionComponent>(_player.LocalEntity.Value, out var UserInteraction)) return;
        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(_player.LocalEntity.Value, out var UserHumanoid)) return;
        SpriteLeft.SetEntity(_player.LocalEntity.Value);
        UserName.Text = $"{Identity.Name(_player.LocalEntity.Value, _eui._entManager, _player.LocalEntity.Value)}\n\n{Loc.GetString($"erp-panel-sex-{UserSex.Value.ToString().ToLowerInvariant()}-text")}";
        UserName.SetOnlyStyleClass(StyleNano.StyleClassLabelSmall);
        foreach (var i in UserInteraction.GenitalSprites)
        {
            if (UserHasClothing) break;
            var t = new TextureRect();
            t.TexturePath = i;
            t.SetSize = new(125, 125);
            t.VerticalAlignment = VAlignment.Top;
            t.HorizontalAlignment = HAlignment.Center;
            t.Stretch = TextureRect.StretchMode.KeepAspectCentered;
            t.Modulate = UserHumanoid.SkinColor;
            t.Margin = new(15);
            TextureLeft.AddChild(t);
        }

        var targets = _entManager.GetEntity(TargetEntityId);
        if (!targets.HasValue) return;
        var target = targets.Value;
        if (!TargetEntityId.HasValue) return;
        if (!TargetSex.HasValue) return;
        if (!_entManager.TryGetComponent<InteractionComponent>(target, out var TargetInteraction)) return;
        if (!_entManager.TryGetComponent<HumanoidAppearanceComponent>(target, out var TargetHumanoid)) return;
        SpriteRight.SetEntity(target);
        TargetName.Text = $"{Identity.Name(target, _eui._entManager, _player.LocalEntity.Value)}\n\n{Loc.GetString($"erp-panel-sex-{TargetSex.Value.ToString().ToLowerInvariant()}-text")}";
        TargetName.SetOnlyStyleClass(StyleNano.StyleClassLabelSmall);
        foreach (var i in TargetInteraction.GenitalSprites)
        {
            if (TargetHasClothing) break;
            var t = new TextureRect();
            t.TexturePath = i;
            t.SetSize = new(125, 125);
            t.VerticalAlignment = VAlignment.Top;
            t.HorizontalAlignment = HAlignment.Center;
            t.Stretch = TextureRect.StretchMode.KeepAspectCentered;
            t.Margin = new(15);
            t.Modulate = TargetHumanoid.SkinColor;
            TextureRight.AddChild(t);
        }
    }

    private void SearchBarOnOnTextChanged(LineEdit.LineEditEventArgs obj)
    {
        PopulateByFilter(SearchBar.Text);
    }
    private List<(string, Texture, InteractionPrototype)> oldItemList = new();
    private void PopulateByFilter(string filter, bool check = true)
    {
        if (!_player.LocalEntity.HasValue) return;
        if (!TargetEntityId.HasValue) return;
        var uid = _player.LocalEntity.Value;
        List<(string, Texture, InteractionPrototype)> itemList = new();
        foreach (var proto in _prototypeManager.EnumeratePrototypes<InteractionPrototype>())
        {
            if(proto.InhandObject.Count > 0)
            {
                if (_entManager.TryGetComponent<HandsComponent>(uid, out var hands))
                {
                    if (hands.ActiveHand == null) continue;
                    if (hands.ActiveHand.Container == null) continue;
                    if (!hands.ActiveHand.Container.ContainedEntity.HasValue) continue;
                    if (!_entManager.TryGetComponent<MetaDataComponent>(hands.ActiveHand.Container.ContainedEntity.Value, out var meta)) continue;
                    if (meta.EntityPrototype == null) continue;
                    if (!proto.InhandObject.Contains(meta.EntityPrototype.ID)) continue;
                }
                else continue;
            }
            if (proto.Erp) continue;
            if (_entManager.GetEntity(TargetEntityId.Value) == _player.LocalEntity.Value && !proto.UseSelf) continue;
            if (string.IsNullOrEmpty(filter) ||
                proto.Name.ToLowerInvariant().Contains(filter.Trim().ToLowerInvariant()))
            {
                var texture = _spriteSystem.Frame0(proto.Icon);
                if (UserHasClothing && proto.UserWithoutCloth) continue;
                if (TargetHasClothing && proto.TargetWithoutCloth) continue;
                if (UserSex != proto.UserSex && proto.UserSex != Sex.Unsexed) continue;
                if (TargetSex != proto.TargetSex && proto.TargetSex != Sex.Unsexed) continue;
                if (!Erp && proto.Erp) continue;
                //ItemInteractions.AddItem(proto.Name, texture, metadata: proto);
                itemList.Add((proto.Name, texture, proto));
            }
        }
        foreach (var proto in _prototypeManager.EnumeratePrototypes<InteractionPrototype>())
        {
            if (proto.InhandObject.Count > 0)
            {
                if (_entManager.TryGetComponent<HandsComponent>(uid, out var hands))
                {
                    if (hands.ActiveHand == null) continue;
                    if (hands.ActiveHand.Container == null) continue;
                    if (!hands.ActiveHand.Container.ContainedEntity.HasValue) continue;
                    if (!_entManager.TryGetComponent<MetaDataComponent>(hands.ActiveHand.Container.ContainedEntity.Value, out var meta)) continue;
                    if (meta.EntityPrototype == null) continue;
                    if (!proto.InhandObject.Contains(meta.EntityPrototype.ID)) continue;
                }
                else continue;
            }
            if (!proto.Erp) continue;
            if (_entManager.GetEntity(TargetEntityId.Value) == _player.LocalEntity.Value && !proto.UseSelf) continue;
            if (string.IsNullOrEmpty(filter) ||
                proto.Name.ToLowerInvariant().Contains(filter.Trim().ToLowerInvariant()))
            {
                var texture = _spriteSystem.Frame0(proto.Icon);
                if (UserHasClothing && proto.UserWithoutCloth) continue;
                if (TargetHasClothing && proto.TargetWithoutCloth) continue;
                if (UserSex != proto.UserSex && proto.UserSex != Sex.Unsexed) continue;
                if (TargetSex != proto.TargetSex && proto.TargetSex != Sex.Unsexed) continue;
                if (!Erp && proto.Erp) continue;
                itemList.Add((proto.Name, texture, proto));
                //ItemInteractions.AddItem(proto.Name, texture, metadata: proto);
            }
        }
        bool equals = true;
        foreach (var i in oldItemList)
        {
            if (!itemList.Contains(i))
            {
                equals = false;
                break;
            }
        }
        foreach (var i in itemList)
        {
            if (!oldItemList.Contains(i))
            {
                equals = false;
                break;
            }
        }
        if (!equals || !check)
        {
            ItemInteractions.Clear();
            foreach(var i in itemList)
            {
                ItemInteractions.AddItem(i.Item1, i.Item2, metadata: i.Item3);
            }
        }


        oldItemList = itemList;
    }
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if(_gameTiming.CurTime > UntilUpdate)
        {
            UntilUpdate = _gameTiming.CurTime + TimeSpan.FromSeconds(1);
            _eui.RequestState();
        }
        _eui.RequestLove();
    }


    public void Populate()
    {
        var prototypes = _prototypeManager.EnumeratePrototypes<InteractionPrototype>().ToList();
        UserDescription.DisposeAllChildren();
        TargetDescription.DisposeAllChildren();
        //Проверки nullable-типов
        if (!TargetEntityId.HasValue) return;
        if (!UserSex.HasValue) return;
        if (!TargetSex.HasValue) return;
        if (!_player.LocalEntity.HasValue) return;

        if (!TargetEntityId.Value.Valid) return;

        //Аминь
        if (Erp)
        {
            //Юзер
            UserDescription.AddChild(new Label { Text = "Вы..." });
            if (UserHasClothing) UserDescription.AddChild(new Label { Text = "...Обладаете одеждой" });
            else UserDescription.AddChild(new Label { Text = "...Не обладаете одеждой" });
            UserDescription.AddChild(new Label { Text = "...Обладаете анусом" });
            if (UserSex.Value == Sex.Male) UserDescription.AddChild(new Label { Text = "...Обладаете пенисом" });
            if (UserSex.Value == Sex.Female) UserDescription.AddChild(new Label { Text = "...Обладаете вагиной" });
            if (UserSex.Value == Sex.Female) UserDescription.AddChild(new Label { Text = "...Обладаете грудью" });
            //Таргет
            if (_entManager.GetEntity(TargetEntityId.Value) != _player.LocalEntity.Value)
            {
                TargetDescription.AddChild(new Label { Text = Identity.Name(_eui._entManager.GetEntity(TargetEntityId.Value), _eui._entManager, _player.LocalEntity.Value) + "..." });
                if (TargetHasClothing) TargetDescription.AddChild(new Label { Text = "...Обладает одеждой" });
                else
                {
                    TargetDescription.AddChild(new Label { Text = "...Не обладает одеждой" });
                    TargetDescription.AddChild(new Label { Text = "...Обладает анусом" });
                    if (TargetSex.Value == Sex.Male) TargetDescription.AddChild(new Label { Text = "...Обладает пенисом" });
                    if (TargetSex.Value == Sex.Female) TargetDescription.AddChild(new Label { Text = "...Обладает вагиной" });
                }
                if (TargetSex.Value == Sex.Female) TargetDescription.AddChild(new Label { Text = "...Обладает грудью" });
            }

        }
        else
        {
            ErpProgress.Dispose();
        }
        if(DescriptionContainer.Visible)
        {
            DescriptionPopulate();
        } else
        {
            PopulateByFilter(SearchBar.Text);
        }
        ItemInteractions.OnItemSelected += _eui.OnItemSelect;
    }
}
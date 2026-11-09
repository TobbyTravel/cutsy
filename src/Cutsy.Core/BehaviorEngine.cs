namespace Cutsy.Core;

public sealed class BehaviorEngine
{
    private const double TickSeconds = 1.0 / 30;
    private readonly Random random;
    private readonly Dictionary<string, double> cooldownUntil = new(StringComparer.Ordinal);
    private ValidatedPack pack;
    private DesktopSnapshot desktop;
    private Behavior behavior;
    private Surface? support;
    private WorldPoint? jumpStart;
    private Surface? jumpTarget;
    private double alongOrigin;
    private double pending;
    private double seconds;
    private double stepAge;
    private double fallSpeed;
    private double graceUntil;
    private int stepIndex;
    private bool stepStarted;
    private bool dragging;
    private bool falling;
    private bool stayHere;
    private WorldPoint stayAnchor;
    private string spriteId;
    private string anchor = "feet";
    private Bounds? clip;
    private SoundRequest? sound;
    private bool interactionSound;
    private bool preview;

    public WorldPoint Contact { get; private set; }
    public string BehaviorId => behavior.Id;
    public double LogicalHeight { get; set; } = 160;
    public double Activity { get; set; } = 1;
    public bool Paused { get; set; }
    public bool Dragging => dragging;
    public bool StayHere
    {
        get => stayHere;
        set
        {
            if (value && !stayHere)
                stayAnchor = Contact;
            stayHere = value;
        }
    }

    public RenderPose Pose => new(spriteId, Contact, HeightAt(Contact), anchor, pack.Sprites[spriteId].Facing, clip);

    public BehaviorEngine(ValidatedPack pack, DesktopSnapshot desktop, int seed)
    {
        this.pack = pack;
        this.desktop = desktop;
        random = new Random(seed);
        behavior = pack.Behaviors[pack.Manifest.DefaultBehaviorId];
        spriteId = FirstSprite(behavior);
        Recover();
    }

    public void Recover()
    {
        dragging = falling = false;
        fallSpeed = 0;
        var area = desktop.Monitors[0].WorkArea;
        var floor = desktop.Surfaces.First(s => s.Kind == SurfaceKind.Floor &&
            s.Start <= area.Right && s.End >= area.X && s.Line == area.Bottom);
        SetSupport(floor, floor.End - HeightAt(floor.Contact(floor.End)) * .65);
        stayAnchor = Contact;
        graceUntil = seconds;
        Start(pack.Behaviors[pack.Manifest.DefaultBehaviorId]);
    }

    public void ReplacePack(ValidatedPack replacement)
    {
        pack = replacement;
        cooldownUntil.Clear();
        behavior = pack.Behaviors[pack.Manifest.DefaultBehaviorId];
        spriteId = FirstSprite(behavior);
        Start(behavior);
    }

    public void SetDesktop(DesktopSnapshot snapshot)
    {
        bool enteringQuiet = !desktop.Quiet && snapshot.Quiet;
        desktop = snapshot;
        if (enteringQuiet)
        {
            CancelDrag();
            sound = null;
            pending = 0;
        }

        if (support is not null)
        {
            var next = UpdatedSurface(support, alongOrigin);
            if (next is null)
            {
                LoseSupport();
                if (!desktop.Quiet)
                    React(Interaction.LostSupport);
            }
            else
            {
                WorldPoint old = Contact;
                SetSupport(next, SurfaceOrigin(next) + alongOrigin);
                if (stayHere || seconds < graceUntil)
                    stayAnchor = new WorldPoint(stayAnchor.X + Contact.X - old.X, stayAnchor.Y + Contact.Y - old.Y);
                support = next;
            }
        }
        if (!dragging && !desktop.Monitors.Any(m => IsOnMonitor(m.WorkArea, Contact)))
            Recover();
    }

    public void Click()
    {
        if (!desktop.Quiet)
            React(Interaction.Click);
    }

    public void BeginDrag(WorldPoint contact)
    {
        if (desktop.Quiet)
            return;
        dragging = true;
        falling = false;
        support = null;
        Contact = contact;
        React(Interaction.Pickup);
    }

    public void DragTo(WorldPoint contact)
    {
        if (dragging)
            Contact = contact;
    }

    public void Drop(WorldPoint contact)
    {
        if (!dragging)
            return;
        dragging = false;
        Contact = contact;
        var snapped = ClosestSupport(contact, 16 * ScaleAt(contact));
        if (snapped is null)
            LoseSupport();
        else
            SetSupport(snapped, Contact.X);
        stayAnchor = Contact;
        graceUntil = seconds + 30;
        React(Interaction.Drop);
    }

    public void CancelDrag()
    {
        if (!dragging)
            return;
        dragging = false;
        LoseSupport();
    }

    public bool Preview(string id)
    {
        if (!pack.Behaviors.TryGetValue(id, out var candidate) || desktop.Quiet || dragging ||
            candidate.RequiredCapabilities.Any(c => !desktop.Capabilities.Contains(c)))
            return false;
        if (candidate.Eligibility.SurfaceKind is { } kind)
        {
            var surface = desktop.Surfaces.FirstOrDefault(s => s.Kind == kind && Fits(s));
            if (surface is null)
                return false;
            SetSupport(surface, (surface.Start + surface.End) / 2);
        }
        Start(candidate, isPreview: true);
        return true;
    }

    public SoundRequest? TakeSound()
    {
        SoundRequest? result = sound;
        sound = null;
        return result;
    }

    public void Advance(TimeSpan elapsed)
    {
        if (desktop.Quiet || Paused)
        {
            pending = 0;
            return;
        }

        pending += Math.Clamp(elapsed.TotalSeconds, 0, .2);
        while (pending >= TickSeconds)
        {
            pending -= TickSeconds;
            Tick();
        }
    }

    private void Tick()
    {
        seconds += TickSeconds;
        if (falling)
            Fall();
        if (dragging && behavior.Eligibility.Interaction != Interaction.Pickup)
            return;

        double remaining = TickSeconds;
        for (int transitions = 0; transitions < 16; transitions++)
        {
            Step step = behavior.Steps[stepIndex];
            if (!stepStarted)
            {
                BeginStep(step);
                stepStarted = true;
            }
            double duration = Duration(step);
            double consume = Math.Min(remaining, Math.Max(0, duration - stepAge));
            stepAge += consume;
            remaining -= consume;
            Execute(step, consume, duration);
            if (stepAge + 1e-9 < duration)
                return;

            if (step.Type == StepType.Jump)
                FinishJump();
            stepStarted = false;
            stepAge = 0;
            if (++stepIndex == behavior.Steps.Length)
            {
                var next = NextBehavior();
                if (dragging)
                {
                    stepIndex = behavior.Steps.Length - 1;
                    stepAge = Duration(behavior.Steps[stepIndex]);
                    stepStarted = true;
                    return;
                }
                Start(next);
            }
            if (remaining <= 1e-9)
                return;
        }
    }

    private void BeginStep(Step step)
    {
        if (step.SpriteId is not null)
            spriteId = step.SpriteId;
        if (step.Anchor is not null)
            anchor = step.Anchor;
        if (step.SoundId is not null && pack.Sounds.ContainsKey(step.SoundId))
            sound = new SoundRequest(step.SoundId, interactionSound || preview);
        if (dragging)
            return;

        switch (step.Type)
        {
            case StepType.Attach when step.Target is { } kind:
                var target = NearbySurface(kind);
                if (target is not null)
                    SetSupport(target, target.Along(Contact));
                break;
            case StepType.Detach:
            case StepType.Fall:
                LoseSupport();
                break;
            case StepType.Jump when !falling:
                jumpStart = Contact;
                jumpTarget = step.Target is { } destination ? NearbySurface(destination) : support;
                support = null;
                break;
        }
        clip = step.Clip ? support?.PeekClip : null;
    }

    private void Execute(Step step, double elapsed, double duration)
    {
        if (!step.Frames.IsDefaultOrEmpty)
        {
            double total = step.Frames.Sum(frame => (double)frame.DurationMs);
            double milliseconds = Math.Max(0, stepAge * 1000 - .0001) % total;
            foreach (var frame in step.Frames)
            {
                if (milliseconds < frame.DurationMs)
                {
                    spriteId = frame.SpriteId;
                    break;
                }
                milliseconds -= frame.DurationMs;
            }
        }
        if (dragging || falling)
            return;

        double height = HeightAt(Contact);
        if (step.Type == StepType.MoveAlongSurface && support is { SupportsFeet: true } surface)
        {
            double coordinate = Contact.X + height * step.Speed * elapsed;
            if (!preview && (stayHere || seconds < graceUntil))
                coordinate = Math.Clamp(coordinate, stayAnchor.X - height * .95, stayAnchor.X + height * .95);
            SetSupport(surface, coordinate);
        }
        if (step.Type == StepType.Jump && jumpStart is { } start && duration > 0)
        {
            double progress = Math.Clamp(stepAge / duration, 0, 1);
            WorldPoint destination = jumpTarget is { } target
                ? target.Contact(ClampTo(target, start.X + step.Speed * height))
                : new WorldPoint(start.X + step.Speed * height, start.Y);
            if (!preview && (stayHere || seconds < graceUntil))
                destination = new WorldPoint(start.X, destination.Y);
            Contact = new WorldPoint(start.X + (destination.X - start.X) * progress,
                start.Y + (destination.Y - start.Y) * progress - step.Height * height * 4 * progress * (1 - progress));
        }
    }

    private void FinishJump()
    {
        if (jumpStart is null)
            return;
        var next = jumpTarget is null ? null : UpdatedSurface(jumpTarget, Contact.X - jumpTarget.Origin.X);
        jumpStart = null;
        jumpTarget = null;
        if (next is not null)
            SetSupport(next, Contact.X);
        else
            LoseSupport();
    }

    private void LoseSupport()
    {
        support = null;
        clip = null;
        anchor = "feet";
        falling = true;
        fallSpeed = 0;
    }

    private void Fall()
    {
        double height = HeightAt(Contact);
        fallSpeed = Math.Min(fallSpeed + height * 10 * TickSeconds, height * 6);
        double y = Contact.Y + fallSpeed * TickSeconds;
        var floor = desktop.Surfaces.Where(s => s.SupportsFeet && Fits(s) &&
            Contact.X >= s.Start && Contact.X <= s.End && s.Line >= Contact.Y - 1 && s.Line <= y)
            .OrderBy(s => s.Line).FirstOrDefault();
        if (floor is not null)
        {
            SetSupport(floor, Contact.X);
            React(Interaction.Land);
        }
        else
        {
            Contact = new WorldPoint(Contact.X, y);
            if (!desktop.Monitors.Any(m => IsOnMonitor(m.WorkArea, Contact)))
                Recover();
        }
    }

    private static bool IsOnMonitor(Bounds area, WorldPoint point) =>
        point.X >= area.X - 1 && point.X <= area.Right + 1 && point.Y >= area.Y - 400 && point.Y <= area.Bottom + 1;

    private void SetSupport(Surface surface, double coordinate)
    {
        support = surface;
        falling = false;
        fallSpeed = 0;
        double clamped = ClampTo(surface, coordinate);
        alongOrigin = clamped - SurfaceOrigin(surface);
        Contact = surface.Contact(clamped);
    }

    private static double SurfaceOrigin(Surface s) => s.Vertical ? s.Origin.Y : s.Origin.X;

    private Surface? UpdatedSurface(Surface previous, double offset) => desktop.Surfaces
        .FirstOrDefault(s => s.Id == previous.Id && s.Kind == previous.Kind &&
            SurfaceOrigin(s) + offset >= s.Start && SurfaceOrigin(s) + offset <= s.End && Fits(s));

    private double ClampTo(Surface surface, double coordinate)
    {
        double padding = Math.Min(HeightAt(surface.Contact(coordinate)) * .38, (surface.End - surface.Start) / 2);
        return Math.Clamp(coordinate, surface.Start + padding, surface.End - padding);
    }

    private bool Fits(Surface s) => s.Clearance >= HeightAt(s.Contact((s.Start + s.End) / 2)) &&
        s.End - s.Start >= HeightAt(s.Contact((s.Start + s.End) / 2)) * .76;

    private Surface? ClosestSupport(WorldPoint contact, double distance) => desktop.Surfaces
        .Where(s => s.SupportsFeet && Fits(s) && contact.X >= s.Start && contact.X <= s.End &&
            Math.Abs(s.Line - contact.Y) <= distance).MinBy(s => Math.Abs(s.Line - contact.Y));

    private Surface? NearbySurface(SurfaceKind kind) => desktop.Surfaces
        .Where(s => s.Kind == kind && Fits(s) &&
            Contact.DistanceSquaredTo(s.Contact(Math.Clamp(s.Along(Contact), s.Start, s.End))) <=
            Math.Pow(HeightAt(Contact) * (preview ? 4 : 1.7), 2))
        .MinBy(s => Contact.DistanceSquaredTo(s.Contact(Math.Clamp(s.Along(Contact), s.Start, s.End))));

    private double ScaleAt(WorldPoint point) =>
        desktop.Monitors.FirstOrDefault(m => point.X >= m.WorkArea.X && point.X < m.WorkArea.Right &&
            point.Y >= m.WorkArea.Y && point.Y <= m.WorkArea.Bottom)?.Scale ??
        desktop.Monitors.MinBy(m => new WorldPoint(m.WorkArea.X + m.WorkArea.Width / 2.0,
            m.WorkArea.Y + m.WorkArea.Height / 2.0).DistanceSquaredTo(point))?.Scale ?? 1;

    private double HeightAt(WorldPoint point) => LogicalHeight * ScaleAt(point);

    private void React(Interaction action)
    {
        var candidate = Choose(pack.SelectionOrder.Where(b => Eligible(b, action)).Select(b => (b, b.SelectionWeight)));
        if (candidate is not null)
            Start(candidate, fromInteraction: action is Interaction.Click or Interaction.Pickup or Interaction.Drop);
    }

    private bool Eligible(Behavior candidate, Interaction? interaction)
    {
        var rule = candidate.Eligibility;
        if (rule.Interaction != interaction || candidate.RequiredCapabilities.Any(c => !desktop.Capabilities.Contains(c)) ||
            cooldownUntil.GetValueOrDefault(candidate.Id) > seconds ||
            (rule.SurfaceKind is { } kind && support?.Kind != kind) ||
            (rule.Grounded is { } grounded && grounded != (support?.SupportsFeet == true)) ||
            (rule.Attached is { } attached && attached != (support is { Kind: not SurfaceKind.Floor })) ||
            (rule.NearbySurface is { } nearby && NearbySurface(nearby) is null) ||
            (rule.Facing is { } facing && pack.Sprites[spriteId].Facing != facing) ||
            rule.MinimumWidth > 0 && (support is null || support.End - support.Start < rule.MinimumWidth * HeightAt(Contact)))
            return false;
        if (interaction is null && candidate.Family != BehaviorFamily.Idle && (falling ||
            seconds < graceUntil || Activity <= 0))
            return false;
        if (candidate.Family == BehaviorFamily.Movement && support is { SupportsFeet: true } surface)
        {
            double delta = candidate.Steps.FirstOrDefault(step => step.Type == StepType.MoveAlongSurface)?.Speed ?? 0;
            double room = delta < 0 ? Contact.X - surface.Start : surface.End - Contact.X;
            if (delta != 0 && room < HeightAt(Contact) * .65)
                return false;
        }
        if (stayHere && candidate.Family == BehaviorFamily.Edge && interaction is null)
            return false;
        return true;
    }

    private Behavior NextBehavior()
    {
        if (behavior.InterruptPolicy == InterruptPolicy.Loop && !preview && Eligible(behavior, null))
            return behavior;
        if (support is { SupportsFeet: false })
            LoseSupport();
        if (!preview)
        {
            var transition = Choose(behavior.Next.Select(next => (pack.Behaviors[next.BehaviorId], next.Weight))
                .Where(choice => Eligible(choice.Item1, null)));
            if (transition is not null)
                return transition;
        }

        if (!falling)
        {
            // Normalize within each eligible family so authoring additional poses does not increase its activity share.
            (BehaviorFamily Family, double Weight)[] families =
            [
                (BehaviorFamily.Idle, 60), (BehaviorFamily.Movement, 20 * Activity),
                (BehaviorFamily.Edge, 15 * Activity), (BehaviorFamily.Reaction, 5 * Activity)
            ];
            var available = pack.SelectionOrder.Where(b => b.SelectionWeight > 0 && Eligible(b, null)).ToLookup(b => b.Family);
            double roll = random.NextDouble() * families.Where(f => available.Contains(f.Family)).Sum(f => f.Weight);
            foreach (var family in families.Where(f => available.Contains(f.Family)))
            {
                roll -= family.Weight;
                if (roll < 0)
                {
                    var next = Choose(available[family.Family].Select(b => (b, b.SelectionWeight)));
                    if (next is not null)
                        return next;
                }
            }
        }
        return pack.Behaviors[pack.Manifest.DefaultBehaviorId];
    }

    private Behavior? Choose(IEnumerable<(Behavior Behavior, double Weight)> choices)
    {
        var available = choices.Where(c => c.Weight > 0).ToArray();
        double roll = random.NextDouble() * available.Sum(c => c.Weight);
        foreach (var choice in available)
        {
            roll -= choice.Weight;
            if (roll < 0)
                return choice.Behavior;
        }
        return null;
    }

    private void Start(Behavior next, bool fromInteraction = false, bool isPreview = false)
    {
        if (jumpStart is not null && support is null)
            LoseSupport();
        behavior = next;
        stepIndex = 0;
        stepAge = 0;
        stepStarted = false;
        jumpStart = null;
        jumpTarget = null;
        interactionSound = fromInteraction;
        preview = isPreview;
        anchor = "feet";
        clip = null;
        cooldownUntil[next.Id] = seconds + next.CooldownMs / 1000.0;
    }

    private static double Duration(Step step) => step.DurationMs > 0
        ? step.DurationMs / 1000.0
        : step.Frames.IsDefaultOrEmpty ? 0 : step.Frames.Sum(f => (double)f.DurationMs) / 1000.0;

    private static string FirstSprite(Behavior idle) => idle.Steps.Select(s =>
        s.SpriteId ?? (s.Frames.IsDefaultOrEmpty ? null : s.Frames[0].SpriteId)).First(s => s is not null)!;
}

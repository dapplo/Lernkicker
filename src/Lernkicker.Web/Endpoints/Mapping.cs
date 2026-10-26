using Lernkicker.Application;
using Lernkicker.Contracts;
using Lernkicker.Domain.Calendar;
using Lernkicker.Domain.Lessons;
using Lernkicker.Domain.Media;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Web.Endpoints;

/// <summary>Maps between the application's models and the HTTP contracts.</summary>
public static class Mapping
{
    public static FamilyOverview ToDto(this FamilySummary f) => new(f.Id, f.Name, f.Parents, f.Children, f.CreatedAt);

    public static Me ToDto(this ParentProfile p) => new(p.Name, p.Email, p.FamilyName, p.IsAdmin, p.Language);

    public static QuizOverview ToDto(this QuizSummary q) => new(q.QuizId, q.Version, q.Title, q.Subject, q.GradeLevel, q.Topic, q.MaxPoints, q.UploadedAt, q.Shared);

    public static ChildOverview ToDto(this ChildSummary c) => new(c.Id, c.Name, c.GradeLevel, c.HasPin, c.Devices, c.IsAdult);

    public static DeviceOverview ToDto(this DeviceSummary d) => new(d.Id, d.Label, d.PairedAt, d.LastUsedAt, d.Locked);

    public static AssignmentOverview ToDto(this AssignmentSummary a) => new(a.Id, a.QuizId, a.Version, a.Title, a.Subject, a.DueOn, a.AssignedAt);

    public static AttemptOverview ToDto(this AttemptSummary a) =>
        new(a.Id, a.QuizId, a.Title, a.Subject, a.Channel, a.SubmittedAt, a.Points, a.MaxPoints, a.Percent, a.Mark);

    public static MyAssignment ToDto(this ChildAssignment a) =>
        new(a.AssignmentId, a.Title, a.Subject, a.Topic, a.MaxPoints, a.DueOn, a.Attempts, a.BestPercent, a.Stars);

    public static DeviceStatusResponse ToDto(this DeviceStatus s) => new(s.Paired, s.ChildName, s.Locked);

    public static CalendarEntryDto ToDto(this CalendarEntry e) =>
        new(e.Uid, e.Kind, e.Title, e.Date, e.StartTime, e.EndTime, e.Location, e.Source, e.EventId, e.ItemId);

    public static SubscriptionOverview ToDto(this SubscriptionSummary s) =>
        new(s.Id, s.Label, s.Host, s.Kind, s.LastSyncedAt, s.LastError, s.EventCount);

    public static LessonOverview ToDto(this LessonSummary l) =>
        new(l.LessonId, l.Title, l.Subject, l.GradeLevel, l.Topic, l.Blocks, l.UploadedAt, l.Shared);

    public static LessonAssignmentOverview ToDto(this LessonAssignmentSummary a) =>
        new(a.Id, a.LessonId, a.Title, a.Subject, a.DueOn, a.AssignedAt, a.PracticeCount, a.LastPracticedAt, a.KnownBlocks, a.Blocks, a.Mode);

    public static TelegramStatusDto ToDto(this TelegramStatus s) => new(s.Enabled, s.BotUsername, s.LinkedAt);

    public static TelegramBotDto ToDto(this TelegramBotSettings s) => new(s.Configured, s.FromFile, s.BotUsername, s.SetAt);

    public static TelegramLinkDto ToDto(this TelegramLinkCreated l) => new(l.Url, l.Code, l.ExpiresAt);

    public static SharedOverview ToDto(this SharedSummary s) =>
        new(s.Id, s.Kind, s.ItemId, s.Title, s.Subject, s.GradeLevel, s.Topic, s.Size, s.SharedAt, s.InMyLibrary);

    public static MyLesson ToDto(this ChildLesson l) => new(l.AssignmentId, l.Title, l.Subject, l.Topic, l.DueOn, l.KnownBlocks, l.Blocks, l.Mode);

    public static LessonDto ToDto(this LessonToLearn l) =>
        new(l.AssignmentId, l.Lesson.Title, l.Lesson.Subject, l.Lesson.Language, l.Lesson.Topic, l.Lesson.Description, l.DueOn,
            l.KnownBlocks, l.Lesson.Blocks.Select(b => ToDto(b, l.Images)).ToList(), l.Lesson.Mode);

    private static LessonBlockDto ToDto(LessonBlock b, IReadOnlyDictionary<string, string> images) => b switch
    {
        TextBlock t => new LessonBlockDto { Id = t.Id, Type = "text", Title = t.Title, Text = t.Text, Image = t.Image.ToDto(images) },
        PoemBlock p => new LessonBlockDto { Id = p.Id, Type = "poem", Title = p.Title, Author = p.Author, Hint = p.Hint, Stanzas = p.Stanzas },
        FlashcardsBlock f => new LessonBlockDto
        {
            Id = f.Id, Type = "flashcards", Title = f.Title,
            Cards = f.Cards.Select(c => new FlashcardDto(c.Front, c.Back, c.FrontImage.ToDto(images), c.BackImage.ToDto(images))).ToList(),
        },
        FactsBlock f => new LessonBlockDto { Id = f.Id, Type = "facts", Title = f.Title, Items = f.Items, Image = f.Image.ToDto(images) },
        ImageBlock i => new LessonBlockDto { Id = i.Id, Type = "image", Title = i.Title, Image = i.Image.ToDto(images) },
        EntriesBlock e => new LessonBlockDto
        {
            Id = e.Id, Type = "entries", Title = e.Title,
            Entries = e.Entries.Select(x => new EntryDto(x.Name, x.Text, x.Image.ToDto(images))).ToList(),
        },
        _ => throw new NotSupportedException($"Unknown block type {b.GetType().Name}"),
    };

    /// <summary>A picture with the link to its stored bytes, or without a link when it is missing.</summary>
    private static ImageDto? ToDto(this ImageRef? image, IReadOnlyDictionary<string, string> images) =>
        image is null ? null : new ImageDto(images.TryGetValue(image.Src, out var hash) ? MediaEndpoints.Url(hash) : null, image.Alt, image.Caption, image.Credit);

    public static EventDetails ToDomain(this EventRequest r) => new(r.Kind, r.Title, r.Date, r.StartTime, r.EndTime, r.Location);

    public static SheetDto ToDto(this QuizSheet s) =>
        new(s.Id, s.Title, s.Subject, s.GradeLevel, s.Language, s.Topic, s.DurationMinutes, s.MaxPoints, s.Exercises.Select(e => ToDto(e, s.Images)).ToList());

    private static ExerciseSheetDto ToDto(ExerciseSheet e, IReadOnlyDictionary<string, string> images)
    {
        List<SheetItemDto> Items(IEnumerable<SheetItem> items) => items.Select(i => new SheetItemDto(i.Index, i.Text, i.Image.ToDto(images))).ToList();

        ExerciseSheetDto sheet = e switch
        {
            ClozeSheet c => new ClozeSheetDto(c.Id, c.Instruction, c.Hint, c.MaxPoints, c.Segments, c.WordBank),
            MatchingSheet m => new MatchingSheetDto(m.Id, m.Instruction, m.Hint, m.MaxPoints, m.Options, Items(m.Rows)),
            OrderingSheet o => new OrderingSheetDto(o.Id, o.Instruction, o.Hint, o.MaxPoints, Items(o.Steps)),
            TrueFalseSheet t => new TrueFalseSheetDto(t.Id, t.Instruction, t.Hint, t.MaxPoints, Items(t.Statements)),
            FreeTextSheet f => new FreeTextSheetDto(f.Id, f.Instruction, f.Hint, f.MaxPoints, f.FieldLabels),
            MultipleChoiceSheet mc => new MultipleChoiceSheetDto(mc.Id, mc.Instruction, mc.Hint, mc.MaxPoints,
                mc.Questions.Select(q => new QuestionSheetDto(q.Index, q.Text, Items(q.Options), q.MultipleAnswers, q.Image.ToDto(images))).ToList()),
            WordSearchSheet w => new WordSearchSheetDto(w.Id, w.Instruction, w.Hint, w.MaxPoints, w.Rows, Items(w.Words)),
            _ => throw new NotSupportedException($"Unknown sheet type {e.GetType().Name}"),
        };
        return sheet with { Image = e.Image.ToDto(images) };
    }

    public static QuizResultDto ToDto(this QuizResult r) =>
        new(r.Points, r.MaxPoints, r.Percent, r.Mark, r.Exercises.Select(e => new ExerciseResultDto(
            e.Id, e.Type, e.Instruction, e.Points, e.MaxPoints,
            e.Parts.Select(p => new PartResultDto(p.Index, p.Correct, p.Solution, p.Explanation, p.ExpectedTruth)).ToList(),
            e.Explanation, e.SolutionText)).ToList());

    public static Dictionary<string, ExerciseAnswer> ToDomain(this Dictionary<string, AnswerDto>? answers) =>
        (answers ?? []).ToDictionary(a => a.Key, a => new ExerciseAnswer
        {
            Texts = a.Value.Texts,
            Truths = a.Value.Truths,
            Order = a.Value.Order,
            Choices = a.Value.Choices,
        });
}

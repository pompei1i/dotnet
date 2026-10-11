/* =====================================================================
   Проєкт №3 — feedback_db: тестові дані.

   Запуск:  mongosh "mongodb://localhost:27017" db/p3/seed.js
   Запускати після collections.js.

   ІДЕМПОТЕНТНІСТЬ: кожен документ вставляється через
     updateOne({ _id }, { $setOnInsert: ... }, { upsert: true })
   тобто спершу перевіряється наявність документа з таким _id, і вставка
   відбувається лише якщо його немає (find-or-create). Повторний запуск
   не створює дублікатів і не перезаписує вже наявні документи.

   courseId / studentId / userId — ті самі Id, що в db/p2/seed.sql
   (курси, викладачі) та db/p1/seed.sql (студенти).
   ===================================================================== */

const database = db.getSiblingDB("feedback_db");

/** Вставляє документи, яких ще немає в колекції; наявні не чіпає. */
function seedOnce(collectionName, documents) {
  const collection = database.getCollection(collectionName);
  let inserted = 0;
  for (const { _id, ...fields } of documents) {
    const result = collection.updateOne({ _id }, { $setOnInsert: fields }, { upsert: true });
    inserted += result.upsertedCount;
  }
  print(`${collectionName}: inserted ${inserted}, skipped ${documents.length - inserted}, total ${collection.countDocuments()}`);
}

// Сталі ідентифікатори, щоб посилання між документами не змінювались від запуску до запуску
const reviewIds = {
  olenaCsharp: ObjectId("670000000000000000000101"),
  sofiiaCsharp: ObjectId("670000000000000000000102"),
  olenaWebApi: ObjectId("670000000000000000000103"),
  dmytroEfCore: ObjectId("670000000000000000000104"),
};
const discussionIds = {
  nextCourse: ObjectId("670000000000000000000201"),
  migrationError: ObjectId("670000000000000000000202"),
  aspireAnnouncement: ObjectId("670000000000000000000203"),
};
const messageIds = {
  nextCourseQuestion: ObjectId("670000000000000000000301"),
  nextCourseAnswer: ObjectId("670000000000000000000302"),
  nextCourseReply: ObjectId("670000000000000000000303"),
  migrationQuestion: ObjectId("670000000000000000000304"),
  migrationAnswer: ObjectId("670000000000000000000305"),
};

/* ---------------------------------------------------------------------
   reviews — чотири документи з РІЗНИМИ наборами полів:
     1) текст + фото + вбудовані коментарі + посилання на обговорення (гібрид)
     2) лише текст
     3) відеовідгук із переліками переваг і недоліків
     4) лише оцінка, без тексту
   --------------------------------------------------------------------- */
seedOnce("reviews", [
  {
    _id: reviewIds.olenaCsharp,
    courseId: 1,                                   // посилання на CoursesDb (без FK)
    courseTitle: "C# для початківців",             // денормалізовано для читання
    author: { studentId: 1, displayName: "Олена Коваль" },   // вбудований об'єкт
    rating: 5,
    text: "Найзрозуміліший курс для старту: багато практики, кожна тема закріплюється завданням.",
    isVerifiedPurchase: true,
    photos: ["reviews/101/certificate.jpg", "reviews/101/final-project.png"],
    comments: [                                    // вбудований масив
      {
        author: { userId: 1, role: "instructor", displayName: "Андрій Мельник" },
        text: "Дякую за відгук! Успіхів із наступним курсом.",
        createdAt: ISODate("2026-09-21T08:30:00Z"),
      },
      {
        author: { userId: 3, role: "student", displayName: "Софія Лисенко" },
        text: "Підтверджую, завдання справді корисні.",
        createdAt: ISODate("2026-09-22T17:05:00Z"),
      },
    ],
    discussionId: discussionIds.nextCourse,        // reference на колекцію discussions
    createdAt: ISODate("2026-09-20T19:00:00Z"),
    updatedAt: ISODate("2026-09-22T17:05:00Z"),
    isDeleted: false,
    version: 3,
  },
  {
    _id: reviewIds.sofiiaCsharp,
    courseId: 1,
    courseTitle: "C# для початківців",
    author: { studentId: 3, displayName: "Софія Лисенко" },
    rating: 4,
    text: "Добрий курс, але хотілося б більше про асинхронність.",
    isVerifiedPurchase: false,
    createdAt: ISODate("2026-10-02T11:20:00Z"),
    isDeleted: false,
    version: 1,
  },
  {
    _id: reviewIds.olenaWebApi,
    courseId: 2,
    courseTitle: "ASP.NET Core Web API",
    author: { studentId: 1, displayName: "Олена Коваль" },
    rating: 5,
    isVerifiedPurchase: true,
    videoUrl: "https://cdn.example.com/reviews/103/review.mp4",
    pros: ["Реальний проєкт від початку до кінця", "Розбір типових помилок", "Швидкі відповіді викладача"],
    cons: ["Мало про тестування"],
    createdAt: ISODate("2026-10-05T16:45:00Z"),
    isDeleted: false,
    version: 1,
  },
  {
    _id: reviewIds.dmytroEfCore,
    courseId: 3,
    courseTitle: "Entity Framework Core на практиці",
    author: { studentId: 4, displayName: "Дмитро Гончар" },
    rating: 4,
    isVerifiedPurchase: false,
    createdAt: ISODate("2026-10-06T09:10:00Z"),
    isDeleted: false,
    version: 1,
  },
]);

/* ---------------------------------------------------------------------
   discussions — гібрид: lastMessage вбудовано (знімок для списку тем),
   а повні повідомлення лежать у messages і знаходяться за discussionId.
   Третій документ — оголошення викладача: інший набір полів (isPinned,
   немає lastMessage, tags і acceptedAnswerId).
   --------------------------------------------------------------------- */
seedOnce("discussions", [
  {
    _id: discussionIds.nextCourse,
    courseId: 1,
    courseTitle: "C# для початківців",
    title: "Чи достатньо цього курсу, щоб перейти до ASP.NET Core?",
    type: "question",
    author: { userId: 3, role: "student", displayName: "Софія Лисенко" },
    status: "resolved",
    tags: ["csharp", "навчальний-план"],
    messagesCount: 3,
    lastMessage: {
      messageId: messageIds.nextCourseReply,
      authorName: "Олена Коваль",
      textPreview: "Я так і зробила: після модуля про класи взяла курс про Web API.",
      createdAt: ISODate("2026-09-24T09:15:00Z"),
    },
    acceptedAnswerId: messageIds.nextCourseAnswer,
    sourceReviewId: reviewIds.olenaCsharp,
    createdAt: ISODate("2026-09-23T10:00:00Z"),
    updatedAt: ISODate("2026-09-24T09:15:00Z"),
  },
  {
    _id: discussionIds.migrationError,
    courseId: 3,
    courseTitle: "Entity Framework Core на практиці",
    title: "Помилка під час застосування міграції",
    type: "question",
    author: { userId: 2, role: "student", displayName: "Максим Ткаченко" },
    status: "open",
    tags: ["ef-core", "migrations"],
    messagesCount: 2,
    lastMessage: {
      messageId: messageIds.migrationAnswer,
      authorName: "Ірина Шевченко",
      textPreview: "Рядок підключення береться з проєкту запуску — вкажіть його параметром.",
      createdAt: ISODate("2026-10-04T13:40:00Z"),
    },
    createdAt: ISODate("2026-10-04T12:05:00Z"),
    updatedAt: ISODate("2026-10-04T13:40:00Z"),
  },
  {
    _id: discussionIds.aspireAnnouncement,
    courseId: 5,
    courseTitle: "Мікросервіси на .NET Aspire",
    title: "Оновлення програми курсу: додано модуль про спостережуваність",
    type: "announcement",
    author: { userId: 3, role: "instructor", displayName: "Тарас Бондаренко" },
    status: "closed",
    messagesCount: 0,
    isPinned: true,
    createdAt: ISODate("2026-10-01T08:00:00Z"),
    updatedAt: ISODate("2026-10-01T08:00:00Z"),
  },
]);

/* ---------------------------------------------------------------------
   messages — referencing: кожне повідомлення посилається на тему
   (discussionId), відповіді — ще й на батьківське повідомлення
   (parentMessageId). Набори полів різні: codeSnippet, attachments,
   reactions, isInstructorAnswer є лише в частини документів.
   --------------------------------------------------------------------- */
seedOnce("messages", [
  {
    _id: messageIds.nextCourseQuestion,
    discussionId: discussionIds.nextCourse,
    author: { userId: 3, role: "student", displayName: "Софія Лисенко" },
    text: "Пройшла курс наполовину. Чи вистачить його, щоб братися за Web API?",
    createdAt: ISODate("2026-09-23T10:00:00Z"),
  },
  {
    _id: messageIds.nextCourseAnswer,
    discussionId: discussionIds.nextCourse,
    parentMessageId: messageIds.nextCourseQuestion,
    author: { userId: 1, role: "instructor", displayName: "Андрій Мельник" },
    text: "Так. Після модуля про класи та інтерфейси можна починати Web API — решту закріпите на практиці.",
    isInstructorAnswer: true,
    reactions: { likes: 4 },
    createdAt: ISODate("2026-09-23T14:30:00Z"),
  },
  {
    _id: messageIds.nextCourseReply,
    discussionId: discussionIds.nextCourse,
    parentMessageId: messageIds.nextCourseAnswer,
    author: { userId: 1, role: "student", displayName: "Олена Коваль" },
    text: "Я так і зробила: після модуля про класи взяла курс про Web API.",
    reactions: { likes: 2 },
    createdAt: ISODate("2026-09-24T09:15:00Z"),
  },
  {
    _id: messageIds.migrationQuestion,
    discussionId: discussionIds.migrationError,
    author: { userId: 2, role: "student", displayName: "Максим Ткаченко" },
    text: "Команда оновлення бази завершується помилкою підключення, хоча застосунок запускається.",
    codeSnippet: { language: "bash", code: "dotnet ef database update" },
    attachments: [{ fileName: "error.png", url: "https://cdn.example.com/messages/304/error.png" }],
    createdAt: ISODate("2026-10-04T12:05:00Z"),
  },
  {
    _id: messageIds.migrationAnswer,
    discussionId: discussionIds.migrationError,
    parentMessageId: messageIds.migrationQuestion,
    author: { userId: 2, role: "instructor", displayName: "Ірина Шевченко" },
    text: "Рядок підключення береться з проєкту запуску — вкажіть його параметром.",
    isInstructorAnswer: true,
    codeSnippet: { language: "bash", code: "dotnet ef database update --startup-project ../Courses.Api" },
    createdAt: ISODate("2026-10-04T13:40:00Z"),
    editedAt: ISODate("2026-10-04T13:45:00Z"),
  },
]);

/* ---------------------------------------------------------------------
   course_ratings — embedding: розподіл оцінок і останні відгуки вбудовані
   в документ курсу. recentReviews — гібрид: короткий знімок відгуку плюс
   reviewId для переходу до повного документа в reviews.
   Значення відповідають AverageRating / ReviewsCount у db/p2/seed.sql.
   --------------------------------------------------------------------- */
seedOnce("course_ratings", [
  {
    _id: 1,
    courseTitle: "C# для початківців",
    reviewsCount: 2,
    averageRating: 4.5,
    distribution: { 1: 0, 2: 0, 3: 0, 4: 1, 5: 1 },
    recentReviews: [
      {
        reviewId: reviewIds.sofiiaCsharp,
        authorName: "Софія Лисенко",
        rating: 4,
        textPreview: "Добрий курс, але хотілося б більше про асинхронність.",
        createdAt: ISODate("2026-10-02T11:20:00Z"),
      },
      {
        reviewId: reviewIds.olenaCsharp,
        authorName: "Олена Коваль",
        rating: 5,
        textPreview: "Найзрозуміліший курс для старту: багато практики.",
        createdAt: ISODate("2026-09-20T19:00:00Z"),
      },
    ],
    updatedAt: ISODate("2026-10-02T11:20:00Z"),
  },
  {
    _id: 2,
    courseTitle: "ASP.NET Core Web API",
    reviewsCount: 1,
    averageRating: 5,
    distribution: { 1: 0, 2: 0, 3: 0, 4: 0, 5: 1 },
    recentReviews: [
      {
        reviewId: reviewIds.olenaWebApi,
        authorName: "Олена Коваль",
        rating: 5,
        createdAt: ISODate("2026-10-05T16:45:00Z"),
      },
    ],
    updatedAt: ISODate("2026-10-05T16:45:00Z"),
  },
  {
    _id: 3,
    courseTitle: "Entity Framework Core на практиці",
    reviewsCount: 1,
    averageRating: 4,
    distribution: { 1: 0, 2: 0, 3: 0, 4: 1, 5: 0 },
    recentReviews: [
      {
        reviewId: reviewIds.dmytroEfCore,
        authorName: "Дмитро Гончар",
        rating: 4,
        createdAt: ISODate("2026-10-06T09:10:00Z"),
      },
    ],
    updatedAt: ISODate("2026-10-06T09:10:00Z"),
  },
]);

print("feedback_db: seed is complete. Expected totals: reviews 4, discussions 3, messages 5, course_ratings 3.");

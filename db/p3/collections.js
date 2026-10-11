/* =====================================================================
   Проєкт №3 — feedback_db (MongoDB, далі чиста архітектура у Лаб. 04)
   Контекст «Feedback»: відгуки про курси, обговорення та рейтинги.

   Запуск:  mongosh "mongodb://localhost:27017" db/p3/collections.js
   Порядок: collections.js -> seed.js
   Скрипт ідемпотентний: наявна колекція не перестворюється (оновлюється
   лише валідатор), createIndex для наявного індексу нічого не робить.

   Колекції та прийоми моделювання:
     reviews        — ГІБРИД: вбудовані автор і коментарі + посилання на discussions
     discussions    — ГІБРИД: вбудований знімок останнього повідомлення + посилання на messages
     messages       — REFERENCING: необмежена кількість повідомлень, посилання на discussions
     course_ratings — EMBEDDING: розподіл оцінок і останні відгуки всередині документа

   Посилання на інші бази (courseId, studentId, userId) — звичайні числові
   поля з Id із CoursesDb / EnrollmentsDb. Жодного зв'язку на рівні СУБД
   між базами немає; цілісність за потреби перевіряє прикладний код.
   ===================================================================== */

const database = db.getSiblingDB("feedback_db");

/** Створює колекцію з валідатором або оновлює валідатор наявної. */
function ensureCollection(name, validator) {
  if (database.getCollectionNames().includes(name)) {
    database.runCommand({ collMod: name, validator, validationLevel: "strict", validationAction: "error" });
    print(`collection ${name}: already exists, validator updated`);
  } else {
    database.createCollection(name, { validator, validationLevel: "strict", validationAction: "error" });
    print(`collection ${name}: created`);
  }
}

// Автор повідомлення/коментаря: вбудований об'єкт.
// displayName — дубльоване поле: для студента власник EnrollmentsDb.Students.FullName,
// для викладача — CoursesDb.Instructors.FullName; оновлюється подіями.
const authorSchema = {
  bsonType: "object",
  required: ["userId", "role", "displayName"],
  properties: {
    userId: { bsonType: ["int", "long"], description: "Id студента (EnrollmentsDb) або викладача (CoursesDb), без FK" },
    role: { enum: ["student", "instructor"] },
    displayName: { bsonType: "string", minLength: 1 },
  },
};

/* ---------------------------------------------------------------------
   reviews — відгук студента про курс. Агрегат «Відгук».
   Валідатор описує лише обов'язкове ядро: решта полів (photos, videoUrl,
   pros, cons, comments, discussionId, text) необов'язкові, тому в одній
   колекції лежать документи з різними наборами полів (гнучка схема).
   --------------------------------------------------------------------- */
ensureCollection("reviews", {
  $jsonSchema: {
    bsonType: "object",
    required: ["courseId", "courseTitle", "author", "rating", "isVerifiedPurchase", "createdAt"],
    properties: {
      courseId: { bsonType: ["int", "long"], description: "посилання на курс у CoursesDb (без FK)" },
      courseTitle: { bsonType: "string", minLength: 1, description: "денормалізовано з CoursesDb.Courses.Title" },
      author: {
        bsonType: "object",
        required: ["studentId", "displayName"],
        properties: {
          studentId: { bsonType: ["int", "long"], description: "посилання на студента в EnrollmentsDb (без FK)" },
          displayName: { bsonType: "string", minLength: 1, description: "денормалізовано з EnrollmentsDb.Students.FullName" },
        },
      },
      rating: { bsonType: "int", minimum: 1, maximum: 5 },
      text: { bsonType: "string", maxLength: 4000 },
      isVerifiedPurchase: { bsonType: "bool", description: "факт із EnrollmentsDb: замовлення у статусі AccessGranted" },
      photos: { bsonType: "array", maxItems: 10, items: { bsonType: "string" } },
      videoUrl: { bsonType: "string" },
      pros: { bsonType: "array", items: { bsonType: "string" } },
      cons: { bsonType: "array", items: { bsonType: "string" } },
      // вбудований масив: коментарів небагато і вони завжди читаються разом із відгуком
      comments: {
        bsonType: "array",
        maxItems: 50,
        items: {
          bsonType: "object",
          required: ["author", "text", "createdAt"],
          properties: {
            author: authorSchema,
            text: { bsonType: "string", minLength: 1, maxLength: 1000 },
            createdAt: { bsonType: "date" },
          },
        },
      },
      discussionId: { bsonType: "objectId", description: "посилання на discussions, якщо розмова переросла коментарі" },
      createdAt: { bsonType: "date" },
      updatedAt: { bsonType: "date" },
      isDeleted: { bsonType: "bool" },
      version: { bsonType: "int", minimum: 1, description: "токен оптимістичної конкурентності" },
    },
  },
});

/* ---------------------------------------------------------------------
   discussions — тема обговорення курсу (питання або оголошення).
   lastMessage — вбудований знімок останнього повідомлення, щоб список тем
   показувався одним запитом; повні повідомлення лежать у messages.
   --------------------------------------------------------------------- */
ensureCollection("discussions", {
  $jsonSchema: {
    bsonType: "object",
    required: ["courseId", "courseTitle", "title", "type", "author", "status", "messagesCount", "createdAt"],
    properties: {
      courseId: { bsonType: ["int", "long"], description: "посилання на курс у CoursesDb (без FK)" },
      courseTitle: { bsonType: "string", minLength: 1, description: "денормалізовано з CoursesDb.Courses.Title" },
      title: { bsonType: "string", minLength: 3, maxLength: 300 },
      type: { enum: ["question", "announcement"] },
      author: authorSchema,
      status: { enum: ["open", "resolved", "closed"] },
      tags: { bsonType: "array", maxItems: 10, items: { bsonType: "string" } },
      messagesCount: { bsonType: "int", minimum: 0 },
      lastMessage: {
        bsonType: "object",
        required: ["messageId", "authorName", "textPreview", "createdAt"],
        properties: {
          messageId: { bsonType: "objectId", description: "посилання на messages" },
          authorName: { bsonType: "string" },
          textPreview: { bsonType: "string", maxLength: 200 },
          createdAt: { bsonType: "date" },
        },
      },
      acceptedAnswerId: { bsonType: "objectId", description: "посилання на messages, лише для вирішених питань" },
      sourceReviewId: { bsonType: "objectId", description: "посилання на reviews, якщо тема виросла з відгуку" },
      isPinned: { bsonType: "bool" },
      createdAt: { bsonType: "date" },
      updatedAt: { bsonType: "date" },
    },
  },
});

/* ---------------------------------------------------------------------
   messages — повідомлення в обговоренні. Окрема колекція з посиланням
   discussionId: повідомлень може бути тисячі, вбудований масив упирався б
   у ліміт документа 16 МБ і перезаписувався б цілком при кожній відповіді.
   --------------------------------------------------------------------- */
ensureCollection("messages", {
  $jsonSchema: {
    bsonType: "object",
    required: ["discussionId", "author", "text", "createdAt"],
    properties: {
      discussionId: { bsonType: "objectId", description: "посилання на discussions" },
      parentMessageId: { bsonType: "objectId", description: "посилання на messages для вкладених відповідей" },
      author: authorSchema,
      text: { bsonType: "string", minLength: 1, maxLength: 5000 },
      isInstructorAnswer: { bsonType: "bool" },
      codeSnippet: {
        bsonType: "object",
        required: ["language", "code"],
        properties: { language: { bsonType: "string" }, code: { bsonType: "string" } },
      },
      attachments: {
        bsonType: "array",
        items: {
          bsonType: "object",
          required: ["fileName", "url"],
          properties: { fileName: { bsonType: "string" }, url: { bsonType: "string" } },
        },
      },
      reactions: { bsonType: "object" },
      createdAt: { bsonType: "date" },
      editedAt: { bsonType: "date" },
    },
  },
});

/* ---------------------------------------------------------------------
   course_ratings — зведений рейтинг курсу, один документ на курс.
   _id дорівнює Id курсу з CoursesDb. Розподіл оцінок і три останні відгуки
   вбудовані: сторінка курсу отримує все одним читанням за _id.
   --------------------------------------------------------------------- */
ensureCollection("course_ratings", {
  $jsonSchema: {
    bsonType: "object",
    required: ["_id", "courseTitle", "reviewsCount", "averageRating", "distribution", "updatedAt"],
    properties: {
      _id: { bsonType: ["int", "long"], description: "Id курсу з CoursesDb (без FK)" },
      courseTitle: { bsonType: "string", minLength: 1, description: "денормалізовано з CoursesDb.Courses.Title" },
      reviewsCount: { bsonType: "int", minimum: 0 },
      averageRating: { bsonType: "number", minimum: 1, maximum: 5 },
      distribution: {
        bsonType: "object",
        required: ["1", "2", "3", "4", "5"],
        description: "кількість відгуків на кожну оцінку",
      },
      recentReviews: {
        bsonType: "array",
        maxItems: 3,
        items: {
          bsonType: "object",
          required: ["reviewId", "authorName", "rating", "createdAt"],
          properties: {
            reviewId: { bsonType: "objectId", description: "посилання на reviews" },
            authorName: { bsonType: "string" },
            rating: { bsonType: "int", minimum: 1, maximum: 5 },
            textPreview: { bsonType: "string", maxLength: 200 },
            createdAt: { bsonType: "date" },
          },
        },
      },
      updatedAt: { bsonType: "date" },
    },
  },
});

/* =====================================================================
   ІНДЕКСИ — кожен створено під конкретний запит.
   ===================================================================== */

// Запит: «відгуки курсу, найновіші перші» (сторінка курсу з пагінацією):
//   find({ courseId }).sort({ createdAt: -1 })
database.reviews.createIndex({ courseId: 1, createdAt: -1 }, { name: "ix_reviews_course_createdAt" });

// Запит: «чи залишав цей студент відгук на курс» + правило: один відгук студента на курс.
//   findOne({ courseId, "author.studentId": studentId })
database.reviews.createIndex(
  { courseId: 1, "author.studentId": 1 },
  { name: "ux_reviews_course_student", unique: true }
);

// Запит: «мої відгуки»; цим же індексом користується обробник події
// StudentRenamed, щоб оновити дубльоване поле author.displayName:
//   find({ "author.studentId": studentId })
database.reviews.createIndex({ "author.studentId": 1 }, { name: "ix_reviews_student" });

// Запит: «відгук, з якого виросло обговорення» (зі сторінки теми назад до відгуку):
//   findOne({ discussionId })
// Частковий індекс: містить лише відгуки, у яких поле discussionId взагалі є.
database.reviews.createIndex(
  { discussionId: 1 },
  { name: "ix_reviews_discussion", partialFilterExpression: { discussionId: { $exists: true } } }
);

// Запит: «теми курсу з обраним статусом, спершу з останньою активністю»:
//   find({ courseId, status }).sort({ updatedAt: -1 })
database.discussions.createIndex(
  { courseId: 1, status: 1, updatedAt: -1 },
  { name: "ix_discussions_course_status_updatedAt" }
);

// Запит: «теми за тегом» (multikey-індекс по масиву):
//   find({ tags: "ef-core" })
database.discussions.createIndex({ tags: 1 }, { name: "ix_discussions_tags" });

// Запит: «повнотекстовий пошук по заголовках тем»:
//   find({ $text: { $search: "міграції" } })
database.discussions.createIndex(
  { title: "text" },
  { name: "ix_discussions_title_text", default_language: "none" }
);

// Запит: «повідомлення теми в хронологічному порядку» (з пагінацією):
//   find({ discussionId }).sort({ createdAt: 1 })
database.messages.createIndex({ discussionId: 1, createdAt: 1 }, { name: "ix_messages_discussion_createdAt" });

// Запит: «відповіді на конкретне повідомлення» (розгортання гілки):
//   find({ parentMessageId })
database.messages.createIndex(
  { parentMessageId: 1 },
  { name: "ix_messages_parent", partialFilterExpression: { parentMessageId: { $exists: true } } }
);

// Запит обробника подій StudentRenamed / InstructorRenamed — оновлення дубльованого displayName:
//   updateMany({ "author.role": role, "author.userId": userId }, { $set: { "author.displayName": name } })
database.messages.createIndex({ "author.role": 1, "author.userId": 1 }, { name: "ix_messages_author" });

// course_ratings читається лише за _id (Id курсу) — додаткові індекси не потрібні.

print("feedback_db: collections and indexes are ready.");
printjson(database.getCollectionNames().sort());

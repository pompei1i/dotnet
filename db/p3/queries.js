/* =====================================================================
   Проєкт №3 — feedback_db: запити перевірки та aggregation pipeline.

   Запуск:  mongosh "mongodb://localhost:27017" db/p3/queries.js
   Скрипт нічого не змінює в базі (вставки в п. 7 свідомо відхиляються).
   ===================================================================== */

const database = db.getSiblingDB("feedback_db");

function section(title) {
  print(`\n--- ${title} ---`);
}

section("1. Відгуки курсу 1, найновіші перші, сторінка 1 по 2 (індекс ix_reviews_course_createdAt)");
printjson(
  database.reviews
    .find({ courseId: 1, isDeleted: false }, { courseTitle: 1, "author.displayName": 1, rating: 1, text: 1, createdAt: 1 })
    .sort({ createdAt: -1 })
    .skip(0)
    .limit(2)
    .toArray()
);

section("2. Гнучка схема: набори полів у документах однієї колекції reviews");
printjson(
  database.reviews
    .aggregate([
      { $project: { fields: { $map: { input: { $objectToArray: "$$ROOT" }, as: "f", in: "$$f.k" } } } },
      { $sort: { _id: 1 } },
    ])
    .toArray()
);

section("3. Відгуки з фото / відеовідгуки / відгуки без тексту");
printjson({
  withPhotos: database.reviews.countDocuments({ photos: { $exists: true } }),
  withVideo: database.reviews.countDocuments({ videoUrl: { $exists: true } }),
  ratingOnly: database.reviews.countDocuments({ text: { $exists: false }, videoUrl: { $exists: false } }),
});

section("4. Aggregation: середня оцінка та кількість відгуків по курсах (звіряється з course_ratings)");
printjson(
  database.reviews
    .aggregate([
      { $match: { isDeleted: false } },
      {
        $group: {
          _id: "$courseId",
          courseTitle: { $first: "$courseTitle" },
          reviewsCount: { $sum: 1 },
          averageRating: { $avg: "$rating" },
          verifiedCount: { $sum: { $cond: ["$isVerifiedPurchase", 1, 0] } },
        },
      },
      { $sort: { averageRating: -1, _id: 1 } },
    ])
    .toArray()
);

section("5. Aggregation з $unwind: хто найчастіше коментує відгуки (вбудований масив comments)");
printjson(
  database.reviews
    .aggregate([
      { $unwind: "$comments" },
      { $group: { _id: { name: "$comments.author.displayName", role: "$comments.author.role" }, comments: { $sum: 1 } } },
      { $sort: { comments: -1, "_id.name": 1 } },
    ])
    .toArray()
);

section("6. Referencing + $lookup: відкриті теми курсу 3 разом із повідомленнями");
printjson(
  database.discussions
    .aggregate([
      { $match: { courseId: 3, status: "open" } },
      {
        $lookup: {
          from: "messages",
          localField: "_id",
          foreignField: "discussionId",
          pipeline: [{ $sort: { createdAt: 1 } }, { $project: { _id: 0, "author.displayName": 1, text: 1, codeSnippet: 1 } }],
          as: "messages",
        },
      },
      { $project: { title: 1, tags: 1, messagesCount: 1, messages: 1 } },
    ])
    .toArray()
);

section("7. Валідатор і унікальний індекс відхиляють некоректні документи");
try {
  database.reviews.insertOne({
    courseId: 4,
    courseTitle: "MongoDB для .NET-розробників",
    author: { studentId: 2, displayName: "Максим Ткаченко" },
    rating: 7,
    isVerifiedPurchase: false,
    createdAt: new Date(),
  });
  print("CHECK FAILED: rating 7 was accepted");
} catch (error) {
  print(`OK, validator rejected rating 7: ${error.message}`);
}
try {
  database.reviews.insertOne({
    courseId: 1,
    courseTitle: "C# для початківців",
    author: { studentId: 1, displayName: "Олена Коваль" },
    rating: 3,
    isVerifiedPurchase: true,
    createdAt: new Date(),
  });
  print("CHECK FAILED: second review of the same student for the same course was accepted");
} catch (error) {
  print(`OK, unique index rejected a duplicate review: ${error.message}`);
}

section("8. Пошук: теми за тегом і повнотекстовий пошук по заголовках");
printjson(database.discussions.find({ tags: "ef-core" }, { title: 1, status: 1 }).toArray());
printjson(database.discussions.find({ $text: { $search: "міграції" } }, { title: 1 }).toArray());

section("9. План запиту: відгуки курсу читаються по індексу, а не скануванням колекції");
const plan = database.reviews.find({ courseId: 1 }).sort({ createdAt: -1 }).explain("queryPlanner").queryPlanner.winningPlan;
printjson(plan);

section("10. Кількість документів (не змінюється після повторного запуску seed.js)");
printjson({
  reviews: database.reviews.countDocuments(),
  discussions: database.discussions.countDocuments(),
  messages: database.messages.countDocuments(),
  course_ratings: database.course_ratings.countDocuments(),
});

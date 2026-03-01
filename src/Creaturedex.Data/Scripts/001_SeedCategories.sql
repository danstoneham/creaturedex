IF NOT EXISTS (SELECT 1 FROM Categories)
BEGIN
    INSERT INTO Categories (Id, Name, Slug, Description, IconName, SortOrder) VALUES
    (NEWID(), 'Dogs',                  'dogs',          'Domestic dog breeds and working dogs',         'dog',       1),
    (NEWID(), 'Cats',                  'cats',          'Domestic cat breeds',                          'cat',       2),
    (NEWID(), 'Small Mammals',         'small-mammals', 'Rabbits, hamsters, guinea pigs, and more',     'rabbit',    3),
    (NEWID(), 'Reptiles & Amphibians', 'reptiles',      'Lizards, snakes, turtles, frogs, and more',    'lizard',    4),
    (NEWID(), 'Birds',                 'birds',         'Parrots, finches, birds of prey, and more',    'bird',      5),
    (NEWID(), 'Fish & Aquatic',        'fish',          'Freshwater, saltwater, and aquarium fish',     'fish',      6),
    (NEWID(), 'Insects & Arachnids',   'insects',       'Beetles, butterflies, spiders, and more',      'bug',       7),
    (NEWID(), 'Farm Animals',          'farm',          'Horses, goats, chickens, and livestock',       'barn',      8),
    (NEWID(), 'Wild Mammals',          'wild-mammals',  'Lions, elephants, wolves, bears, and more',    'paw',       9),
    (NEWID(), 'Ocean Life',            'ocean',         'Whales, dolphins, sharks, and sea creatures',  'waves',    10),
    (NEWID(), 'Primates',              'primates',      'Monkeys, apes, and lemurs',                    'monkey',   11);
END

using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using boboddyv2_api.Models;

namespace boboddyv2_api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AcronymController : Controller
    {


        private const string StartSymbol = "^";
        private const string EndSymbol = "$";
        private const string NormSymbol = "%";
        private const double Penalty = -10;
        private const int MaxSequences = 250;
        private const int TotalSequences = 5;
        private const double InitialSimilarity = 0.1;
        private const double SimilarityIncrement = 0.1;

        private IMemoryCache _cache;

        public AcronymController(IMemoryCache cache)
        {
            _cache = cache;
        }

        [HttpGet]
        public IEnumerable<string> Get(string word, string data)
        {
            List<string> acronyms = generateAcronym(word, data);

            return acronyms; // Json(new { data = acronyms });
        }

        private List<string> generateAcronym(string word, string dataKey)
        {
            IDictionary<string, IDictionary<string, int>> WordF1;
            IDictionary<string, IDictionary<string, int>> WordF2;
            IDictionary<string, IDictionary<string, int>> PosF1;
            IDictionary<string, IDictionary<string, int>> PosF2;
            IDictionary<string, IDictionary<string, IEnumerable<string>>> PosWord;

            (WordF1, WordF2, PosF1, PosF2, PosWord) = _cache.Get<AcronymModel>(dataKey).GetData();

            Dictionary<List<(string, string)>, double> SequenceScores = new Dictionary<List<(string, string)>, double>();

            IDictionary<string, int> StartWordF1 = WordF1[StartSymbol];

            IDictionary<string, int> StartPosF1 = PosF1[StartSymbol];


            double PosScore;
            double WordScore;
            double ComplexityScore;

            foreach (string adjPos in PosF1[StartSymbol].Keys)
            {
                if (adjPos != NormSymbol && PosWord[adjPos].ContainsKey(word[0].ToString()))
                {
                    foreach (string adjWord in PosWord[adjPos][word[0].ToString()])
                    {
                        if (adjWord != word && (RareLetterCheck(word[0]) || StartWordF1.ContainsKey(adjWord))) // Need rare letter check
                        {
                            PosScore = GetScore(StartPosF1, adjPos);
                            WordScore = GetScore(StartWordF1, adjWord);
                            ComplexityScore = GetComplexity(adjWord);

                            List<(string, string)> NewSequence = new List<(string, string)>();
                            NewSequence.Add((StartSymbol, StartSymbol));
                            NewSequence.Add((adjWord, adjPos));

                            SequenceScores[NewSequence] = PosScore + WordScore + ComplexityScore;
                        }
                    }
                }
            }

            SequenceScores = FilterTopSequences(SequenceScores);

            IDictionary<string, int> PosF1End = null;
            IDictionary<string, int> PosF2End = null;

            for (int i = 1; i < word.Length; i++)
            {
                Dictionary<List<(string, string)>, double> NextSequenceScores = new Dictionary<List<(string, string)>, double>();

                bool IsNearEnd = (i == word.Length - 2) || (i == word.Length - 1);

                foreach (List<(string, string)> sequence in SequenceScores.Keys)
                {
                    (string, string) WordA = sequence[sequence.Count - 2];
                    (string, string) WordB = sequence[sequence.Count - 1];

                    IDictionary<string, int> ScopedWordF1 = WordF1[WordB.Item1];
                    IDictionary<string, int> ScopedWordF2 = WordF2[WordA.Item1];
                    IDictionary<string, int> ScopedPosF1 = PosF1[WordB.Item2];
                    IDictionary<string, int> ScopedPosF2 = PosF2[WordA.Item2];

                    IDictionary<string, int> FilterWordF1 = ScopedWordF1
                                                                  .OrderByDescending(pair => pair.Value)
                                                                  .Take(Math.Max(ScopedWordF1.Count * 2 / 3, 150))
                                                                  .ToDictionary(pair => pair.Key, pair => pair.Value);

                    foreach (string adjPos in ScopedPosF1.Keys)
                    {
                        if (adjPos != NormSymbol && PosWord[adjPos].ContainsKey(word[i].ToString()))
                        {
                            if (IsNearEnd)
                            {
                                PosF1End = PosF1[adjPos];
                                PosF2End = PosF2[adjPos];
                            }

                            foreach (string adjWord in PosWord[adjPos][word[i].ToString()])
                            {
                                if (RareLetterCheck(word, i - 1, i) || FilterWordF1.ContainsKey(adjWord))
                                {
                                    double TotalScore = SequenceScores[sequence];

                                    if (i == word.Length - 2)
                                    {
                                        TotalScore += GetScore(WordF2[adjWord], EndSymbol);
                                        TotalScore += GetScore(PosF2End, EndSymbol);
                                    }
                                    else if (i == word.Length - 1)
                                    {
                                        TotalScore += GetScore(WordF1[adjWord], EndSymbol);
                                        TotalScore += GetScore(PosF1End, EndSymbol);
                                    }

                                    PosScore = GetScore(ScopedPosF1, adjPos) + GetScore(ScopedPosF2, adjPos);
                                    WordScore = GetScore(ScopedWordF1, adjWord) + GetScore(ScopedWordF2, adjWord);
                                    ComplexityScore = GetComplexity(adjWord);

                                    TotalScore += PosScore + WordScore + ComplexityScore;

                                    List<(string, string)> NextSequence = new List<(string, string)>(sequence);
                                    NextSequence.Add((adjWord, adjPos));
                                    NextSequenceScores[NextSequence] = TotalScore;
                                }
                            }
                        }
                    }
                }

                SequenceScores = (i != word.Length - 1) ? FilterTopSequences(NextSequenceScores) : NextSequenceScores
                                                                                        .OrderByDescending(pair => pair.Value)
                                                                                        .ToDictionary(pair => pair.Key, pair => pair.Value);
            }

            List<List<string>> AcronymSequences = new List<List<string>>();
            double SimilarityLimit = InitialSimilarity;

            while (AcronymSequences.Count < TotalSequences && SimilarityLimit <= 1)
            {
                List<List<string>> NewAcronyms = GetAcronyms(SequenceScores.Take((int)(5000 * SimilarityLimit)).ToDictionary(pair => pair.Key, pair => pair.Value), AcronymSequences, SimilarityLimit);

                int AddIndex = 0;
                while (AcronymSequences.Count < TotalSequences && AddIndex < NewAcronyms.Count)
                {
                    AcronymSequences.Add(NewAcronyms[AddIndex++]);
                }
                SimilarityLimit += SimilarityIncrement;
            }

            List<string> Acronyms = AcronymSequences.ConvertAll(seq => String.Join(" ", seq));

            return Acronyms;
        }

        private double GetScore(IDictionary<string, int> graphRef, string wordRef)
        {
            return graphRef.ContainsKey(wordRef) ? Math.Log10((double)graphRef[wordRef] / graphRef[NormSymbol]) : Penalty;
        }

        private Dictionary<List<(string, string)>, double> FilterTopSequences(IDictionary<List<(string, string)>, double> SequenceScores)
        {
            return SequenceScores
                      .OrderByDescending(pair => pair.Value)
                      .Take(MaxSequences)
                      .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        private List<List<string>> GetAcronyms(IDictionary<List<(string, string)>, double> SequenceScores, List<List<string>> topAcronyms, double SimilarityLimit)
        {
            List<List<string>> NewAcronyms = new List<List<string>>();
            while (SequenceScores.Count > 0)
            {

                List<(string, string)> key = SequenceScores.First().Key;
                SequenceScores.Remove(key);

                List<string> NewAcronym = key.Skip(1).ToList().ConvertAll(pair => pair.Item1);

                bool Pass = true;
                foreach (List<string> acronym in topAcronyms.Concat(NewAcronyms))
                {
                    if (SentenceSimilarity(acronym, NewAcronym) > SimilarityLimit)
                    {
                        Pass = false;
                        break;
                    }
                }
                if (Pass)
                {
                    NewAcronyms.Add(NewAcronym);
                }
            }
            return NewAcronyms;
        }

        private double SentenceSimilarity(List<string> currAcro, List<string> newAcro)
        {
            double SimilarityValue = 0;
            double TotalValue = 0;
            double CurrentValue = 0;

            for (int i = 0; i < currAcro.Count; i++)
            {
                CurrentValue = GetComplexity(newAcro[i]);
                TotalValue += CurrentValue;

                if (currAcro[i] == newAcro[i])
                {
                    SimilarityValue += CurrentValue;
                }
            }

            return SimilarityValue / TotalValue;
        }

        private int GetComplexity(string word)
        {
            char[] Vowels = { 'a', 'e', 'i', 'o', 'u' };

            bool IsPreviousVowel = false;
            int Syllables = 0;

            foreach (char c in word)
            {
                if (Vowels.Contains(c))
                {
                    if (!IsPreviousVowel)
                    {
                        Syllables++;
                    }
                    IsPreviousVowel = true;
                }
                else
                {
                    IsPreviousVowel = false;
                }
            }

            return Syllables;
        }

        private bool RareLetterCheck(char c)
        {
            return (c == 'x') || (c == 'z');
        }

        private bool RareLetterCheck(string word, int StartSymbol, int EndSymbol)
        {
            for (int i = StartSymbol; i <= EndSymbol; i++)
            {
                if (RareLetterCheck(word[i])) return true;
            }
            return false;
        }
    }
}
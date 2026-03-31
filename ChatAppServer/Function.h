#ifndef Function
#include <iostream>
#include <string>
#include <iomanip>
#include "Huffman.h"
#include <chrono>
#include <thread>
#define ONE 1.0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000;
using namespace std;
double number;

void Compress(void)
{
    std::cin.precision(std::numeric_limits<long double>::max_digits10);
    Character* a = new Character[256];
    int number;
    double time = 0;
    string text;
    read(number, a, time, text);
    //cout << "Number is: " << number << endl;
    GenerateCode(number, a, time);
    SelfInformation(number, a);
    DescendingOrder(number, a);
    /*cout << "\t\t\t";
    cout << "Letter" << "\t" << "Probability" << "\t\t" << "Self information" << "\t\t" << "Code" << endl;
    cout << "\t\t\t";
    for (int i = 0; i < 35; i++)
    {
        cout << "--";
    }
    cout << endl;
    for (int i = 0; i < number; i++)
    {
        int checker = 0;
        cout << "\t\t\t";
        if (a[i].character == " ")
        {
            cout << "Space"<<"\t";
            checker++;
        }
        if (a[i].character == "\n")
        {
            cout << "|n"<<"\t";
            checker++;
        }
        if (checker == 0)
        {
            cout << a[i].character << "\t";
        }
        cout << a[i].probability << "\t\t";
        cout << a[i].SelfInfo << "\t\t\t";
        cout << a[i].code << "\t" << endl;
    }
    cout << "\t\t\t";
    for (int i = 0; i < 35; i++)
    {
        cout << "--";
    }
    cout << endl;*/
    // single value
    cout << "\t\t\t\t";
    cout << "H(X) = " << Entropy(number, a) << "\t\t";
    cout << "R = " << AverageBitRate(number, a) << "\t";
    cout << "Efficiency = " << (Entropy(number, a) / AverageBitRate(number, a)) * 100 << "%" << "\t" << endl;
    PrintToFile(number, a, Entropy(number, a), AverageBitRate(number, a), (Entropy(number, a) / AverageBitRate(number, a)), text);
    cout << "The number of source letters is: " << number << endl;
    ofstream Output1;
    Output1.open("OutputText.txt");
    Output1 << text << endl;
    cout << "Compressing" << endl;
    string BinaryString = StringToBinary(text, number, a, time);
    string CompressedText = Compressor(BinaryString, a, number, time);
    string HuffmanTable;
    for (int i = 0; i < number; i++)
    {
        HuffmanTable += static_cast<string>(a[i].character);
        HuffmanTable += " ";
        HuffmanTable += a[i].code;
    }
    //Record Huffman's Table into .txt file:
    //cout << BinaryString << endl;   
    Output1.close();
    //cout << "Compressed Data is: " << CompressedText << endl;
    cout << "The process has been executed" << endl;
    cout << "Compression ratio is: " << ((static_cast<double>(CompressedText.length()) + static_cast<double>(HuffmanTable.length())) / static_cast<double>(text.length())) * 100 << "%" << endl;
    cout << "The compressed file has been saved as CompressedText.txt" << endl;
    cout << "Press Enter to exit!" << endl;
    cout << "Total processing time: " << time << endl;
    std::cin.ignore();
    cin.get();
}

#endif // Function

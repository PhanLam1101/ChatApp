#ifndef HUFFMAN_HEADER
#include <iostream>
#include <fstream>
#include <cmath>
#include<string>
#include<vector>
#include <chrono>
#include<ctime>
#include <bitset>
#include <unordered_map>
#include <iomanip>
#include "Tool.h"
#pragma warning(disable : 4996)
#define ZERO  0.000000000000000000000000000000000000000000000000000000000000000000000000000000000
using namespace std;
string ASCII_plus[36] = { "00","01","02","03","04","05","06","07",
"08","0a","0b","0c","0d","0e","0f","0g","0h","0i","0j","0k","0l",
"0m","0n","0o","0p","0q","0r","0s","0t","0u","0v","0w","0x","0y","0z", "0A" };

class Character {
public:
	string character;
	long double probability;
	string code;
	int root1;//smallest
	int root2;//secondSmalltest
	long double SelfInfo;
	Character(void)
	{
		probability = ZERO;
	}
	Character(char character_, int probability_, bool position_)
	{
		character = character_;
		probability = probability_;
	}Character(char character_, int probability_)
	{
		character = character_;
		probability = probability_;
	}
	void DefaultEnterValue(int i)
	{
		character = "x" + to_string(i);
		for (int j = 1; j > 0; j++)
		{
			cout << "character '" << character << "' :";
			cin >> probability;
			if (probability > 0 && probability <= 1)
			{
				j = -1;
			}
			else if (probability <= 0 || probability > 1)
			{
				cout << "The probability cannot be negative or greater than 1!" << endl;
			}
		}
	}
	long double ReturnProbability()
	{
		return this->probability;
	}
	string ReturnCharacter()
	{
		return this->character;
	}
	Character operator = (Character temp)
	{
		this->probability = temp.ReturnProbability();
		this->character = temp.ReturnCharacter();
	}
};
void PrintList(int n, Character a[])
{
	for (int i = 0; i < n; i++)
	{
		cout << "Character: " << a[i].character << "\t" << a[i].probability << "\t";
		cout << a[i].code << endl;
	}
}
void PrintToFile(int number, Character a[], long double Entropy, long double Rate, long double Efficiency, string text_)
{
	ofstream Output;
	Output.open("TextHistory.txt", ios::app);
	Output << "*\n";
	auto now = std::chrono::system_clock::now();
	// Convert the time point to a time_t object
	std::time_t currentTime = std::chrono::system_clock::to_time_t(now);

	// Convert the time_t object to a local time
	std::tm* localTime = std::localtime(&currentTime);

	// Output the current date and time
	Output << "Generated Code at date and time: ";
	Output << (localTime->tm_year + 1900) << '-';
	Output << (localTime->tm_mon + 1) << '-';
	Output << localTime->tm_mday << ' ';
	Output << localTime->tm_hour << ':';
	Output << localTime->tm_min << ':';
	Output << localTime->tm_sec << endl;
	Output << "* Here is the text: " << endl;
	Output << text_;
	Output << "Length of the text is: " << text_.length() << endl;
	Output << "\t\t\t\t";
	Output << "Letter" << "\t\t" << "Probability" << "\t" << "Self information" << "\t" << "Code" << endl;
	Output << "\t\t\t\t";
	Output.precision(5);
	for (int i = 0; i < 35; i++)
	{
		Output << "--";
	}
	Output << endl;
	for (int i = 0; i < number; i++)
	{
		int checker = 0;
		Output << "\t\t\t\t";
		if (a[i].character == " ")
		{
			Output << "Space" << "\t";
			checker++;
		}
		if (a[i].character == "\n")
		{
			Output << "|n" << "\t";
			checker++;
		}
		if (checker == 0)
		{
			Output << a[i].character << "\t";
		}
		Output << a[i].ReturnProbability() << "\t\t";
		Output << a[i].SelfInfo << "\t\t\t";
		Output << a[i].code << "\t" << endl;
	}
	Output << "\t\t\t\t";
	for (int i = 0; i < 35; i++)
	{
		Output << "--";
	}
	Output << endl;
	// single value
	Output << "\t\t\t\t";
	Output << "H(X) = " << Entropy << "\t\t";
	Output << "R = " << Rate << "\t";
	Output << "Efficiency = " << Efficiency * 100 << "%" << "\t" << endl;
}
long double Entropy(int number, Character a[])
{
	setprecision(20);
	long double entropy = 0;
	if (number == 1)
	{
		return 1;
	}
	else if (number > 1)
	{
		for (int i = 0; i < number; i++)
		{
			entropy += -(a[i].probability * log2(a[i].probability));
		}
	}
	return entropy;
}
long double AverageBitRate(int number, Character a[])
{
	long double R = 0;
	if (number == 1)
	{
		return 1;
	}
	else if (number > 1)
	{
		for (int i = 0; i < number; i++)
		{
			R += a[i].probability * ((long double)a[i].code.length());
		}
	}
	return R;
}
void SelfInformation(int number, Character a[])
{
	if (number == 1)
	{
		a[0].SelfInfo = 0;
	}
	else if (number > 1)
	{
		for (int i = 0; i < number; i++)
		{
			a[i].SelfInfo = -log2(a[i].probability);
		}
	}
}
void GenerateCode(int n, Character a[], double& time_)
{
	auto Start = std::chrono::high_resolution_clock::now();
	if (n == 1)
	{
		a[0].code = "1";
	}
	else if (n > 1)
	{
		int quantity = 0;
		Character temp[1000];
		//Find number of nodes:
		for (int i = 0; i < n; i++)
		{
			string name;
			name = "x" + to_string(i);
			temp[i].probability = a[i].probability;
			temp[i].character = name;
			/*cout << "character :" << temp[i].character << " " << temp[i].probability << endl;*/
			quantity++;
		}

		long double smallest, secondSmallest;
		int smallestIndex = 0, secondIndex = 1;
		smallest = temp[0].probability;
		secondSmallest = temp[1].probability;
		for (int j = 0; j < n; j++)
		{
			for (int i = 0; i < n; i++)
			{
				if (temp[i].probability < smallest) {
					secondSmallest = smallest;
					secondIndex = smallestIndex;
					smallest = temp[i].probability;
					smallestIndex = i;
				}
				else if (temp[i].probability < secondSmallest && temp[i].probability > smallest) {
					secondSmallest = temp[i].probability;
					secondIndex = i;
				}
			}
		}
		for (int i = 0; i < n; i++)
		{
			if (smallest == temp[i].probability && smallestIndex != i)
			{
				secondSmallest = temp[i].probability;
				secondIndex = i;
			}
		}
		/*cout << "Smallest is: " << smallest << "\t at" << smallestIndex << endl;
		cout << "Second smallest is: " << secondSmallest << "\t at" << secondIndex << endl;*/
		temp[n].character = "x" + to_string(smallestIndex) + "/" + "x" + to_string(secondIndex);
		temp[n].probability = smallest + secondSmallest;
		temp[n].root1 = smallestIndex;
		temp[n].root2 = secondIndex;
		//number of nodes: 
		int number = SumOfSeries2(n);
		int count = 2;
		int additional_nodes = 1;
		/*int *exception_index= new int[number];*/
		int exception_index[1000];
		exception_index[0] = smallestIndex;
		exception_index[1] = secondIndex;
		/*cout << "Number is: " << number << endl;*/
		for (int i = n; i < number - 1; i++)
		{
			int try_index = 0;
			for (int j = 0; j < n + additional_nodes; j++)
			{
				bool isException = false;
				for (int l = 0; l < number; l++)
				{
					if (j == exception_index[l])
					{
						isException = true;
					}
				}
				if (isException)
				{
					continue;
				}
				if (try_index == 0)
				{
					smallest = temp[j].probability;
					smallestIndex = j;
					try_index++;
				}
				else if (try_index == 1)
				{
					secondSmallest = temp[j].probability;
					secondIndex = j;
					try_index++;
				}
			}
			/*cout << "pre-Smallest is: " << smallest << "\t at" << smallestIndex << endl;
			cout << "pre-Second smallest is: " << secondSmallest << "\t at" << secondIndex << endl;
			cout << "loop " << i << endl;
			for (int j = 0; j < count; j++)
			{
				cout << "exception: " << *(exception_index + j) << endl;
			}*/
			for (int j = 0; j < n + additional_nodes; j++)
			{
				for (int k = 0; k < n + additional_nodes; k++)
				{
					bool isException = false;
					for (int l = 0; l < count; l++)
					{
						if (k == exception_index[l])
						{
							isException = true;
						}
					}
					if (isException)
					{
						continue;
					}
					if (temp[k].probability < smallest) {
						secondSmallest = smallest;
						secondIndex = smallestIndex;
						smallest = temp[k].probability;
						smallestIndex = k;
					}
					else if (temp[k].probability < secondSmallest && temp[k].probability > smallest) {
						secondSmallest = temp[k].probability;
						secondIndex = k;
					}
				}
			}
			int index_temp1 = smallestIndex;
			int index_temp2 = secondIndex;
			for (int j = 0; j < n + additional_nodes; j++)
			{
				bool isException = false;
				for (int l = 0; l < count; l++)
				{
					if (j == exception_index[l] || j == index_temp1)
					{
						isException = true;
					}
				}
				if (isException)
				{
					continue;
				}
				if (temp[j].probability < secondSmallest)
				{
					secondSmallest = temp[j].probability;
					secondIndex = j;
					break;
				}
			}
			additional_nodes++;
			if (count + 2 <= number)
			{
				count += 2;
				exception_index[count - 2] = smallestIndex;
				exception_index[count - 1] = secondIndex;
			}
			else if (count + 2 > number)
			{
				count += 1;
				exception_index[count - 1] = smallestIndex;
			}
			temp[i + 1].probability = smallest + secondSmallest;
			temp[i + 1].character = temp[secondIndex].character + "/" + temp[smallestIndex].character;
			temp[i + 1].root1 = smallestIndex;
			temp[i + 1].root2 = secondIndex;
			/*cout << "Smallest is: " << smallest << "\t at" << smallestIndex << endl;
			cout << "Second smallest is: " << secondSmallest << "\t at" << secondIndex << endl;*/
		}
		//cout << "List of characters: " << endl;
		/*for (int i = 0; i < number; i++)
		{
			cout << temp[i].character << " :\t" << temp[i].probability << endl;
		}*/
		//greater is 0, smaller is 1
		for (int i = 0; i < n; i++)
		{
			//cout <<"* " << temp[i].character << " :" << endl;
			int k = number - 1;
			for (int j = number - 1; j >= n; j--)
			{
				/*cout << "Code : " << temp[i].code << endl;*/
				int r1 = temp[k].root1;
				int r2 = temp[k].root2;
				string bin1 = "1";
				string bin0 = "0";
				if (k < n)
				{
					break;
				}
				if (FindVariable(temp[r1].character, temp[i].character) == true)
				{
					temp[i].code += bin1;
					k = r1;
				}
				if (FindVariable(temp[r2].character, temp[i].character) == true)
				{
					temp[i].code += bin0;
					k = r2;
				}
			}
		}
		for (int i = 0; i < n; i++)
		{
			/*cout << "character :" << temp[i].character << "\t" << temp[i].probability << endl;
			cout << "Code : " << temp[i].code << endl;*/
			a[i].code = temp[i].code;
		}
	}
	auto End = std::chrono::high_resolution_clock::now();
	std::chrono::duration<double> Duration = End - Start;
	time_ += static_cast<double>(Duration.count());
}
//void read(int &number, Character character[], double& time_, string& text_)
//{
//	string A;
//	/*vector<char>String(1000000);*/
//	int count = 0;
//	cout << "Enter a text ";
//	// Prompt the user to enter the A
//std::cout << "(press ''Enter'' twice to finish):\n";
//// Input A line by line until an empty line is encountered
//int numberEmptyLines = 0;
//while (true) {
//    std::string line;
//	
//    std::getline(std::cin, line);
//    // Check if the line is empty (i.e., just Enter is pressed)
//    if (line.empty()) {
//		numberEmptyLines++;
//    }
//	else
//	{
//		numberEmptyLines = 0;
//	}
//	if (numberEmptyLines < 2)
//	{
//		A += line + "\n"; // Add newline character to maintain the format
//	}
//	if (numberEmptyLines == 2)
//	{
//		break;
//	}
//    // Append the line to the A
//   
//}
//	vector<char> String(A.begin(), A.end());
//// Display the input A
////std::cout << "\nYou entered:\n" << A << std::endl;
//	int size = A.length();
//	cout << endl;
//	cout << "Number of characters is: " << A.length()<<endl;
//	int* possibleNumber;
//	possibleNumber = new int[size];
//	auto Start = chrono::high_resolution_clock::now();
//	for (int i = 0; i < size; i++)
//	{
//		char a;
//		for (int j = 0; j < 255; j++)
//		{
//			a = j;
//			if (a == String[i])
//			{
//				int count = 0;
//				for (int k = 0; k < size; k++)
//				{
//					if (a == possibleNumber[k])
//						count++;
//				}
//				if (count == 0)
//					possibleNumber[i] = a;
//				else
//					possibleNumber[i] = 0;
//			}
//		}
//	}
//	int n = 0;
//	for (int i = 0; i < size; i++)
//	{
//		if (possibleNumber[i] != 0)
//		{
//			char a = possibleNumber[i];
//			/*cout << "There is character " << a << " in this line";*/
//			int count = 0;
//			for (int j = 0; j < size; j++)
//			{
//				if (a == String[j])
//					count++;
//			}
//			/*cout << " and there is/are " << count << endl;*/
//			character[n].character = a;
//			character[n].probability = static_cast<long double>(count)/ static_cast<long double>(A.length());
//			n++;
//		}
//	}
//	int numberOfCharacters=0;
//	for (int i = 0; i < size; i++)
//	{
//		if (possibleNumber[i] != 0)
//		{
//			numberOfCharacters++;
//		}
//	}
//	number = numberOfCharacters;
//	text_ = A;
//	auto End = chrono::high_resolution_clock::now();
//	chrono::duration<double> Duration = End - Start;
//	//std::cout << "Time taken: " << Duration.count() << " seconds" << std::endl;
//	time_ = static_cast<double>(Duration.count());
//}
void read(int& number, Character character[], double& time_, string& text_)
{
	std::cin.precision(std::numeric_limits<long double>::max_digits10);
	Character a[256];
	/*vector<char>String(1000000);*/
	int count = 0;
	vector<char> c(text_.begin(), text_.end());
	// Display the input A
	//std::cout << "\nYou entered:\n" << A << std::endl;
	cout << endl;
	cout << "Number of characters is: " << text_.length() << endl;
	auto Start = chrono::high_resolution_clock::now();
	int freq = 0;
	int size = text_.length();
	for (int i = 0; i <= 255; i++)
	{
		if (i != 127)
		{
			for (int j = 0; j < size; j++)
			{
				char temp_c;
				temp_c = static_cast<char>(i);
				if (c[j] == temp_c)
				{
					freq++;
				}
				if (i == 255)
				{
					text_ += c[j];
				}
			}
			//cout << "Character " << static_cast<char>(i) << " has appeared" << freq << " times" << endl;
			if (freq != 0)
			{
				char temp_c;
				temp_c = static_cast<char>(i);
				long double frequency = static_cast<long double>(freq);
				long double numberOfCharacters = static_cast<long double>(size);
				if (temp_c == ' ')
				{
					freq = freq - 1;
					a[count].character = "spc";
					a[count].probability = frequency / numberOfCharacters;
				}
				else if (temp_c == '\t')
				{
					a[count].character = "|t";
					a[count].probability = frequency / numberOfCharacters;
				}
				else if (temp_c == '\n')
				{
					a[count].character = "|n";
					a[count].probability = frequency / numberOfCharacters;
				}
				else if (temp_c != ' ' && temp_c != '\n' && temp_c != '\t')
				{
					a[count].character += static_cast<char>(i);
					a[count].probability = frequency / numberOfCharacters;
				}
				count++;
			}
			freq = 0;
		}
	}
	number = count;
	for (int i = 0; i < number; i++)
	{
		character[i].character = a[i].character;
		character[i].probability = a[i].probability;
	}
	auto End = chrono::high_resolution_clock::now();
	chrono::duration<double> Duration = End - Start;
	//std::cout << "Time taken: " << Duration.count() << " seconds" << std::endl;
	time_ = static_cast<double>(Duration.count());
}
std::string StringToBinary(const std::string& text, int number, Character a[], double& time_) {
	std::string compressedData;
	auto Start = chrono::high_resolution_clock::now();
	vector<char> String(text.begin(), text.end());
	for (int j = 0; j < text.length(); j++)
	{
		if (String[j] != ' ' && String[j] != '\t' && String[j] != '\n')
		{
			for (int i = 0; i < number; i++)
			{
				string character_;
				character_ += String[j];
				if (character_ == a[i].character)
				{
					compressedData += a[i].code;
				}
			}
		}
		else if (String[j] == ' ')
		{
			for (int i = 0; i < number; i++)
			{
				string character_;
				character_ += "spc";
				if (character_ == a[i].character)
				{
					compressedData += a[i].code;
				}
			}
		}
		else if (String[j] == '\t')
		{
			for (int i = 0; i < number; i++)
			{
				string character_;
				character_ += "|t";
				if (character_ == a[i].character)
				{
					compressedData += a[i].code;
				}
			}
		}
		else if (String[j] == '\n')
		{
			for (int i = 0; i < number; i++)
			{
				string character_;
				character_ += "|n";
				if (character_ == a[i].character)
				{
					compressedData += a[i].code;
				}
			}
		}
	}
	auto End = chrono::high_resolution_clock::now();
	chrono::duration<double> Duration = End - Start;
	//std::cout << "Time taken: " << Duration.count() << " seconds" << std::endl;
	time_ += static_cast<double>(Duration.count());
	return compressedData;
}
string Compressor(string BinaryString, Character a[], int number, double& time_)
{
	auto Start = chrono::high_resolution_clock::now();
	ofstream output;
	output.open("CompressedText.txt", ios::binary);
	for (int i = 0; i < number; i++)
	{
		if (a[i].character == " ")
		{
			output << "spc" << " " << a[i].code << endl;
		}
		else if (a[i].character == "\n")
		{
			output << "|n" << " " << a[i].code << endl;
		}
		else if (a[i].character == "\t")
		{
			output << "|t" << " " << a[i].code << endl;
		}
		else
		{
			output << a[i].character << " " << a[i].code << endl;
		}
	}
	output << "####" << endl;
	string CompressedText;
	int IntegerBuffer;
	/*int Loop;
	if (BinaryString.length() % 8 == 0)
	{
		Loop = BinaryString.length() / 8;
	}
	else if (BinaryString.length() % 8 != 0)
	{
		Loop = BinaryString.length() / 8 + 1;
	}*/
	string Buffer;
	int counter = 0;
	int i = 0;
	for (char c : BinaryString)
	{
		i++;
		Buffer += c;
		counter++;
		if (counter == 8)
		{
			/*cout << "Buffer: " << Buffer;*/
			int temp = BinaryStringToInteger(Buffer);
			//cout << "\t" << temp << endl;
			if (temp <= 33 || temp == 127 || temp == 48)
			{
				string temp_c;
				if (temp == 48)
				{
					temp_c = "00";
				}
				else if (temp == 127)
				{
					temp_c = "0A";
				}
				else if (temp <= 33)
				{
					temp_c = ASCII_plus[temp + 1];
				}
				CompressedText += temp_c;
				//output.put(static_cast<char>(temp));
				output << temp_c;
				counter = 0;
				Buffer.clear();
			}
			else if (temp > 32 && temp != 48 && temp != 127)
			{
				//output.put(static_cast<char>(temp));
				output << static_cast<char>(temp);
				CompressedText += static_cast<char>(temp);
				Buffer.clear();
				counter = 0;
			}
		}
		else if (counter < 8 && i == BinaryString.length() - 1)
		{
			/*cout << "Buffer: " << Buffer;*/
			int temp = BinaryStringToInteger(Buffer);
			/*cout << "\t" << temp << endl;*/
			if (temp <= 32 || temp == 127 || temp == 48)
			{
				string temp_c;
				if (temp == 48)
				{
					temp_c = "00";
				}
				else if (temp == 127)
				{
					temp_c = "0A";
				}
				else if (temp <= 32)
				{
					temp_c = ASCII_plus[temp + 1];
				}
				CompressedText += temp_c;
				counter = 0;
				/*output.put(static_cast<char>(temp));*/
				output << temp_c;
				Buffer.clear();
			}
			else if (temp > 32 && temp != 48 && temp != 127)
			{
				output << static_cast<char>(temp);
				CompressedText += static_cast<char>(temp);
				Buffer.clear();
				counter = 0;
			}
		}
	}
	output.close();
	auto End = chrono::high_resolution_clock::now();
	chrono::duration<double> Duration = End - Start;
	//std::cout << "Time taken: " << Duration.count() << " seconds" << std::endl;
	time_ += static_cast<double>(Duration.count());
	return CompressedText;
}
void DescendingOrder(int number, Character a[])
{
	Character* b = new Character[256];
	for (int i = 0; i < number; i++)
	{
		b[i].character = a[i].character;
		b[i].probability = a[i].probability;
		b[i].code = a[i].code;
	}
	for (int i = 0; i < number; i++)
	{
		for (int j = 0; j < number; j++)
		{

			if (b[i].probability > b[j].probability)
			{
				Character temp;
				temp.probability = b[j].probability;
				b[j].probability = b[i].probability;
				b[i].probability = temp.probability;
				temp.code = b[j].code;
				b[j].code = b[i].code;
				b[i].code = temp.code;
				temp.character = b[j].character;
				b[j].character = b[i].character;
				b[i].character = temp.character;
			}
		}
	}
	SelfInformation(number, b);
	cout << "\t\t\t";
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
		if (b[i].character == " ")
		{
			cout << "Space" << "\t";
			checker++;
		}
		if (b[i].character == "\n")
		{
			cout << "|n" << "\t";
			checker++;
		}
		if (checker == 0)
		{
			cout << b[i].character << "\t";
		}
		cout << setprecision(8) << b[i].probability << "\t\t";
		cout << setprecision(8) << b[i].SelfInfo << "\t\t\t";
		cout << b[i].code << "\t" << endl;
	}
	cout << "\t\t\t";
	for (int i = 0; i < 35; i++)
	{
		cout << "--";
	}
	cout << endl;
}
#endif // !HUFFMAN_HEADER
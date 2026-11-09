const fs=require('node:fs');
const inventory=JSON.parse(fs.readFileSync(__dirname+'/inventory.json','utf8'));
const lines=String.raw`
0	Find a tour
1	For example: Da Nang, Phu Quoc...
2	Beautiful Vietnam
3	View photos of Vietnam
4	Previous tour
5	Next tour
6	Previous offer
7	Next offer
8	Destinations by region
9	Enjoy your journey with Future Travel
10	Over 1,000 tours. Start exploring.
11	Great prices - 24/7 support - destinations everywhere
12	Budget
13	Choose a price range
14	Under VND 5 million
15	VND 5-10 million
16	VND 10-20 million
17	Over VND 20 million
18	TOUR SEARCH RESULTS
19	Clear filters
20	No matching tours yet. Try another destination or budget.
21	EXPLORE FUTURE TRAVEL
22	No featured tours available yet.
23	SPECIAL TOUR OFFERS
24	No special offers are currently available. Explore our featured tours above.
25	FAVOURITE DESTINATIONS
26	Explore
27	Travel with confidence
28	The best reasons to choose
29	our travel company
30	We work to make your travel dreams a reality, with memorable journeys and unmissable sights.
31	We have
32	5+ years
33	of experience
34	Popular destinations
35	Happy travellers
36	Explore destinations
37	Home
38	Enjoy professional travel services, wonderful moments and memorable journeys. We are here to help you discover the world at its best.
39	Northern Vietnam
40	Central Vietnam
41	Southern Vietnam
42	1,000+ tours
43	Quality domestic and international journeys
44	10K+ five-star reviews
45	From travellers who have booked with us
46	100+ offers every day
47	For early bookings, groups and last-minute trips
48	Departing from:
49	View tour
50	No upcoming departures
51	 ₫
52	Contact us
53	Breadcrumb
54	Tour photos
55	Enlarge tour photo
56	Previous month
57	Next month
58	Prices and departure information
59	Tour photo gallery
60	Close photo
61	Previous photo
62	Next photo
63	Travel
64	The selected departure is no longer available. Please choose another date below.
65	View photos
66	Tour photos are being updated.
67	DEPARTURE DATES
68	No upcoming departures yet. You can explore the tour, itinerary and contact options below.
69	Choose a month
70	Choose a suitable date to see prices and transport details.
71	Transport details
72	Transport information is being updated.
73	Passenger prices
74	Prices are being updated. Please contact us for advice.
75	Passenger
76	Age / Notes
77	Original price
78	Price
79	MORE ABOUT THIS TOUR
80	ITINERARY
81	The detailed itinerary for this tour has not been updated yet.
82	/ person
83	Tour code:
84	Departure date:
85	Choose a departure date
86	Booking assistance
87	Contact us for help choosing the right departure date and price.
88	OTHER TOURS
89	View details →
90	×
91	{0} seats available
92	Adult
93	Child
94	Infant
95	Single-room supplement
96	Attractions
97	Meals
98	Suitable for
99	Best time to travel
100	Transport
101	Accommodation
102	Sold out
103	Sold out
104	Outbound
105	Inbound
106	Being updated
107	Please choose a departure date
108	Book tour
109	Choose a date to book
110	Choose an available departure
111	Where would you like to go?
112	Search by tour name, code or destination
113	Remove budget filter
114	Remove transport filter
115	Remove destination filter
116	Remove departure location filter
117	Sort tours
118	Tour pagination
119	Previous page
120	Next page
121	Find your perfect trip
122	Over 1,000 tours waiting to be explored
123	Search
124	tours
125	Newest
126	Price: low to high
127	Price: high to low
128	Name: A-Z
129	Sort
130	Destination
131	All destinations
132	Departure location
133	All departure locations
134	All
135	Apply filters
136	Clear all
137	No tours found
138	Try another keyword or change your filters
139	View all tours
140	From
141	Details
142	Find tours
143	Selected destination
144	selected location
145	Tour not found
146	This tour no longer exists or is no longer available.
147	Explore other tours →
148	IMPORTANT INFORMATION
149	The policy for this departure is being updated. Please contact us for assistance.
150	What's included
151	What's not included
152	Payment terms
153	Cancellation terms
154	Booking code:
155	A transaction requires review, possibly because payment arrived after the booking expired. Please do not pay again. Contact support with your booking code to confirm the payment or arrange a refund.
156	The seats have been released. Please choose a departure to make a new booking. If your account was charged, contact support for a payment review.
157	Your payment is confirmed and your seats are reserved. Please keep your booking code.
158	Total:
159	Sandbox · Test payment
160	Pay for your booking
161	Payment deadline:
162	Time remaining to pay:
163	Your previous payment was unsuccessful. You can try again before the booking expires.
164	Your transaction has not been confirmed yet. If you have completed payment on VNPAY, select “Check payment”.
165	VNPAY payment is being configured. Please contact support.
166	Pay with VNPAY
167	You will be redirected to VNPAY Sandbox to pay using test payment details.
168	The payment deadline has passed or less than one minute remains. We are checking the result before releasing the seats.
169	VNPAY transaction
170	Transaction reference:
171	Check payment
172	Contact information
173	Explore other trips
174	Call 1900 2045
175	Book and pay
176	Paid
177	Cancelled
178	Payment expired
179	Awaiting payment
180	Awaiting confirmation · Unpaid
181	Payment needs review
182	Payment successful
183	Your reservation has expired
184	Booking cancelled
185	Your booking request has been received
186	Enter the contact person's full name
187	Contact phone number
188	Enter your contact address
189	Enter the passenger's full name
190	Any requests or information you would like to share...
191	Booking summary
192	Tours
193	/ Book tour
194	BOOK YOUR TOUR
195	ENTER DETAILS
196	SUBMIT REQUEST
197	AWAIT CONFIRMATION
198	This departure is sold out, closed for bookings or has no prices yet.
199	Choose another departure
200	Please check your details
201	CONTACT DETAILS
202	We have filled in your details. You can edit them before submitting.
203	You can book without an account, or
204	sign in to fill in your details faster
205	Full name
206	Phone
207	Address
208	PASSENGERS
209	Ages are calculated on the departure date. Each adult can accompany one infant. Infants do not use a reserved seat.
210	seats
211	PASSENGER DETAILS
212	Remove
213	Full name *
214	Gender *
215	Date of birth *
216	NOTES
217	Notes for your trip
218	PRICE SUMMARY
219	Total
220	Prices depend on the departure and passenger type. After booking, your seats are held until the deadline shown on the payment page.
221	I have read and agree to the
222	tour policies
223	← Back to tour details
224	Female
225	Other
226	Aged 12 and over
227	Aged 2-11
228	Under 2 years old
229	Price unavailable
230	Unable to verify the payment result
231	The returned information is invalid or does not match the transaction. This link has not confirmed a payment.
232	Open the booking page in the browser you used to book and check the status. If your account was charged, contact support with your booking code.
233	Find a tour
234	Contact support
235	Unable to verify payment
236	Booking page unavailable
237	Choose a tour
238	Booking information
239	Access denied
240	This area is for administrators only.
241	Back to home
242	Administrator sign-in
243	Access denied
244	Enter your password
245	Show password
246	Administration system
247	Sign in with an administrator account to manage tours, locations and departures.
248	Explore Vietnam,
249	discover every destination
250	Continue exploring and find the right trip with Future Travel.
251	Password
252	Remember me
253	Customer sign-in
254	Sign up now
255	Admin sign-in
256	Sign in
257	Discover destinations
258	From north to south, islands to mountains
259	Find your ideal journey
260	Clear tour and departure information
261	Find travel offers
262	Explore tours at great prices
263	Administrator access
264	With you on every journey
265	Please use an account with administrator access.
266	Welcome back! Continue your journey.
267	Administrator email
268	Open dashboard
269	Alex Nguyen
270	At least 8 characters
271	Enter your password again
272	Show confirmation password
273	Join the journey,
274	share your passion for travel
275	Create a free account and start exploring.
276	Create an account
277	Start your next journey with Future Travel.
278	Full name
279	Phone number
280	At least 8 characters, including uppercase, lowercase and a number.
281	Confirm password
282	Create account
283	Sign in now
284	Explore Vietnam
285	Find your favourite destinations and journeys
286	A wide choice of departures
287	Choose the right time for your trip
288	Travel with Future
289	Help and advice for your trip
290	Discover tours, travel offers and your favourite destinations with Future Travel.
291	Future Travel - Home
292	Open menu
293	Main navigation
294	Coming soon
295	Skip to main content
296	✈ Flights
297	Activities
298	Train tickets
299	With you on every journey.
300	About Future
301	About us
302	Help
303	Products
304	Tours
305	Tour offers
306	Favourite destinations
307	Domestic tours
308	Payment partners
309	Connect with us
310	· Future Vietnam Co., Ltd. · Tax ID: 0313580179
311	Admin dashboard
312	Sign out
313	Sign up
314	The date of birth does not match the ticket type. Age is calculated on the departure date.
315	passenger
316	Please reduce the number of infants first. Each adult can accompany only one infant.
317	The passenger count is invalid or exceeds the available seats.
318	Submitting…
319	Hide password
320	Processing…
321	The time to start a payment has expired
322	Please enter your email
323	Please enter a valid email address
324	Please enter your password
325	Please enter your full name
326	Full name must be between 3 and 100 characters
327	Full name can contain only letters and spaces
328	Phone number must contain 10-11 digits
329	Password must be between 8 and 72 characters
330	Password must contain uppercase, lowercase and a number
331	Please enter your password again
332	Passwords do not match
333	Password is too long. Please use no more than 72 UTF-8 bytes.
334	Please enter the contact person's full name
335	Please enter a phone number
336	Please enter your contact address
337	Notes must not exceed 2,000 characters
338	Please confirm you have read the tour policies
339	Please add a passenger
340	A booking can contain up to 50 passengers
341	Please enter the passenger's name
343	Invalid gender
344	Please enter a date of birth
345	Invalid passenger type
346	Plane
347	Coach
348	Train
349	Cruise
350	Incorrect email or password.
351	Your account is locked. Please contact an administrator.
352	Your email has not been verified. Please contact support.
353	This account does not have administrator access.
354	This email is already registered. Please use another email or sign in.
355	Your booking session is invalid. Please reload the page.
356	This departure is no longer accepting bookings. Please choose another date.
357	The price or departure has changed. Please review your details and total before submitting again.
358	Each booking must contain 1-50 passengers.
359	At least one adult must be included in the group.
360	Each adult can accompany up to one infant under 2.
361	Only {0} seats remain. Please reduce the passenger count or choose another departure.
362	Passenger {0} has an invalid date of birth.
363	Passenger {0}'s age does not match the ticket type on the departure date.
364	No valid price is available for passenger type {0}.
365	Booking not found.
366	This booking is no longer awaiting payment.
367	The payment deadline has passed or is too close. Please check the booking status.
368	The booking amount cannot be paid through VNPAY. Please contact support.
369	A payment has already been recorded for this booking. Please check the payment result.
370	The transaction details have changed. Please contact support for a review.
371	Several payment attempts have failed. Please contact support.
372	Unable to sign in. Please try again.
373	Your account has been created. You can sign in now.
374	Please choose a tour and departure before booking.
375	No matching tour and departure were found.
376	The departure information is invalid.
377	This departure is no longer available. Please choose another date.
378	Your booking session has expired or your account has changed. Please review your details and submit again.
379	The payment status has been checked. If you have just paid, please allow some time for the result to update.
380	The filters are invalid. Please check the destination and page number.
381	Your payment result has been received. The status below is verified through VNPAY notifications or a direct enquiry.
382	Language
`.trim().split('\n');
const translations={};
for(const line of lines){const match=line.match(/^(\d+)\t(.+)$/);if(!match)throw Error('Invalid translation line: '+line);const index=Number(match[1]);translations[inventory[index].key]=match[2];}
for(const [source,english] of Object.entries({
 'Nam':'Male','Liên hệ & hỗ trợ':'Contact & support','Ngôn ngữ':'Language','Bạn chưa có tài khoản?':'New here?',
 'Chưa có tài khoản?':'New here?','Đã có tài khoản?':'Already have an account?',
 'Bạn cần tư vấn thêm về chuyến đi?':'Need help with your trip?',
 'Tìm thấy {0} chuyến đi.':'{0} tours found.','Hiển thị {0} chuyến đi mới nhất.':'Showing the {0} newest tours.',
 'Chưa có điểm đến tại {0}.':'No destinations available in {0} yet.',
 'Tiết kiệm {0}':'Save {0}','Hiển thị {0}–{1} trên {2} chuyến đi':'Showing {0}–{1} of {2} tours',
 'Từ {0}':'From {0}','Chọn lịch {0}, {1}, còn {2} chỗ':'Choose {0}, {1}, {2} seats available',
 'Xem chi tiết {0}':'View details for {0}','Xem ảnh {0}':'View photo {0}','{0} - ảnh {1}':'{0} - photo {1}',
 'Tháng {0} năm {1}':'{0}/{1}','THÁNG {0}/{1}':'{0}/{1}',
 'Ngày {0}: {1}':'Day {0}: {1}','Lưu trú: {0}':'Accommodation: {0}',
 'Còn {0} chỗ · Đang chọn':'{0} seats available · Selected',
 'Giảm số {0}':'Reduce {0} count','Thêm {0}':'Add {0}','Xoá hành khách {0}':'Remove passenger {0}',
 'Xoá {0} {1}':'Remove {0} {1}','Giờ Việt Nam':'Vietnam time','(giờ Việt Nam)':'(Vietnam time)',
 'Mã giao dịch VNPAY:':'VNPAY transaction number:','Ngân hàng:':'Bank:',
 'Thời gian thanh toán:':'Payment time:',
 'Bạn thao tác quá nhiều lần. Vui lòng thử lại sau một phút.':'Too many attempts. Please try again in one minute.',
 'T2':'Mon','T3':'Tue','T4':'Wed','T5':'Thu','T6':'Fri','T7':'Sat','CN':'Sun','tr':'m',
 'Thanh toán':'PAYMENT','Xác nhận':'CONFIRMATION','Tiết kiệm':'Save','Tại sao chọn Future?':'Why choose Future?',
 'Khởi hành':'Departure','Lưu trú:':'Accommodation:','Giá từ:':'From:','Giá người lớn:':'Adult price:',
 'Ngày khởi hành:':'Departure:','Hành khách: {0} người':'Passengers: {0}','Ghi chú:':'Notes:',
 'Bộ lọc':'Filters','(không bắt buộc)':'(optional)',
 'Chính sách bảo mật':'Privacy policy',
 'Chính sách bảo mật đang được cập nhật. Vui lòng liên hệ để được hỗ trợ.':'Our privacy policy is being updated. Please contact us for assistance.'
}))translations[source]=english;
fs.writeFileSync('D:/TourismMvc/TourismMvc/Localization/user.en.json',JSON.stringify(translations,null,2)+'\n');
console.log('Saved '+Object.keys(translations).length+' English translations.');
